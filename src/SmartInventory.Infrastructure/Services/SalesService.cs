using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Constants;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Services;

public class SalesService : ISalesService
{
    private const int MaxLines = 100;
    private const int MaxQuantityPerLine = 10_000;

    private readonly InventoryDbContext _db;

    public SalesService(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<CheckoutResultDto> CheckoutAsync(
        CheckoutRequestDto request,
        string cashier,
        CancellationToken ct = default)
    {
        // ---- 1. Validate the request shape (never trust the client) --------------------------
        var items = request.Items ?? [];
        if (items.Count == 0)
            return Fail(CheckoutErrorCodes.Invalid, "The cart is empty.");

        if (items.Any(i => i.Quantity <= 0 || i.Quantity > MaxQuantityPerLine))
            return Fail(CheckoutErrorCodes.Invalid, $"Quantities must be between 1 and {MaxQuantityPerLine:N0}.");

        // The same product may arrive twice: merge it. Sum as long so it can't overflow.
        // Sorting by id gives every concurrent checkout the same lock order, which prevents deadlocks.
        var wanted = items
            .GroupBy(i => i.ProductId)
            .Select(g => (Id: g.Key, Qty: g.Sum(x => (long)x.Quantity)))
            .OrderBy(x => x.Id)
            .ToList();

        if (wanted.Count > MaxLines)
            return Fail(CheckoutErrorCodes.Invalid, $"A sale can have at most {MaxLines} different products.");

        if (wanted.Any(w => w.Qty > MaxQuantityPerLine))
            return Fail(CheckoutErrorCodes.Invalid, $"Quantities must be between 1 and {MaxQuantityPerLine:N0}.");

        // ---- 2. Load the products once: name, SKU, PRICE and reorder level come from the DB ---
        var ids = wanted.Select(w => w.Id).ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var errors = wanted
            .Where(w => !products.ContainsKey(w.Id))
            .Select(w => new CheckoutErrorDto(CheckoutErrorCodes.NotFound, "A product in the cart no longer exists.", w.Id))
            .ToList();
        if (errors.Count > 0)
            return CheckoutResultDto.Failed(errors);

        // ---- 2b. Idempotency: the same SaleId must never be recorded twice -----------------------
        var receipt = request.SaleId is { } saleId ? ReceiptFrom(saleId) : ReceiptFrom(Guid.NewGuid());
        if (request.SaleId is not null
            && await _db.StockTransactions.AnyAsync(t => t.ReferenceNumber == receipt, ct))
        {
            return Fail(CheckoutErrorCodes.Duplicate, $"This sale was already recorded as receipt {receipt}.");
        }

        // ---- 3. One database transaction for the whole sale ------------------------------------
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var (id, qtyLong) in wanted)
        {
            var qty = (int)qtyLong;

            // Atomic "check and decrement" in a single SQL statement:
            //   UPDATE "Products" SET "currentStock" = "currentStock" - @qty
            //   WHERE "Id" = @id AND "currentStock" >= @qty
            // Two cashiers selling the last unit at the same moment cannot both win: Postgres locks the
            // row, the second statement re-checks the WHERE clause and matches 0 rows.
            var affected = await _db.Products
                .Where(p => p.Id == id && p.currentStock >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.currentStock, p => p.currentStock - qty), ct);

            if (affected == 0)
            {
                var available = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => p.currentStock)
                    .FirstOrDefaultAsync(ct);

                var name = products[id].Name;
                errors.Add(new CheckoutErrorDto(
                    CheckoutErrorCodes.InsufficientStock,
                    available <= 0 ? $"{name} is out of stock." : $"Only {available} of {name} left.",
                    id,
                    Math.Max(0, available)));
            }
        }

        // Report EVERY short product at once, then undo the lines that did succeed.
        if (errors.Count > 0)
        {
            await tx.RollbackAsync(ct);
            return CheckoutResultDto.Failed(errors);
        }

        // ---- 4. Immutable ledger rows (one per product, all sharing the receipt number) --------
        var now = DateTime.UtcNow;

        foreach (var (id, qty) in wanted)
        {
            _db.StockTransactions.Add(new StockTransaction
            {
                ProductId = id,
                Type = TransactionType.StockOut,
                Quantity = -(int)qty, // outflows are negative in the ledger
                ReferenceNumber = receipt,
                Notes = $"POS sale · {cashier}",
                PerformedBy = cashier,
                TimestampUtc = now
            });
        }

        // ---- 5. Low-stock alerts ---------------------------------------------------------------
        // Read the stock AFTER our updates (same transaction, so we see our own writes).
        var remaining = await _db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.currentStock, ct);

        var openAlerts = await _db.InventoryAlerts
            .Where(a => ids.Contains(a.ProductId) && !a.IsResolved && a.AlertType == AlertTypes.LowStock)
            .ToListAsync(ct);

        foreach (var (id, _) in wanted)
        {
            var product = products[id];
            var left = remaining[id];
            if (left > product.ReorderLevel) continue;

            var severity = left <= 0 ? AlertSeverity.Critical : AlertSeverity.Warning;
            var message = left <= 0
                ? $"{product.Name} is out of stock."
                : $"{product.Name} is down to {left} (reorder level {product.ReorderLevel}).";

            // One OPEN alert per product: refresh it instead of raising a new one on every sale.
            var existing = openAlerts.FirstOrDefault(a => a.ProductId == id);
            if (existing is null)
            {
                _db.InventoryAlerts.Add(new InventoryAlert
                {
                    ProductId = id,
                    AlertType = AlertTypes.LowStock,
                    Message = message,
                    Severity = severity
                });
            }
            else
            {
                existing.Message = message;
                if (severity > existing.Severity) existing.Severity = severity;
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // ---- 6. Build the receipt from server-side values -------------------------------------
        var lines = wanted
            .Select(w =>
            {
                var p = products[w.Id];
                var qty = (int)w.Qty;
                var left = remaining[w.Id];
                return new CheckoutLineDto(p.Id, p.Sku, p.Name, qty, p.UnitPrice, p.UnitPrice * qty, left, left <= p.ReorderLevel);
            })
            .OrderBy(l => l.Name)
            .ToList();

        return new CheckoutResultDto(true, receipt, lines.Sum(l => l.LineTotal), lines, []);
    }

    private static CheckoutResultDto Fail(string code, string message) =>
        CheckoutResultDto.Failed(new CheckoutErrorDto(code, message));

    /// <summary>Deterministic from the id, so a retry of the same sale maps to the same receipt number.</summary>
    private static string ReceiptFrom(Guid id) => $"S-{id.ToString("N")[..10].ToUpperInvariant()}";
}
