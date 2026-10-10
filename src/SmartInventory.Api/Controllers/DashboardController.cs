using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Api.Models.ViewModels;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Api.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly InventoryDbContext _db;

    public DashboardController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var products = _db.Products.AsNoTracking();

        // Sequential on purpose: a DbContext instance must not run concurrent queries.
        var totalProducts = await products.CountAsync(ct);
        var totalUnits = await products.SumAsync(p => (int?)p.currentStock, ct) ?? 0;
        var inventoryValue = await products.SumAsync(p => (decimal?)(p.currentStock * p.UnitPrice), ct) ?? 0m;
        var lowStockCount = await products.CountAsync(p => p.currentStock <= p.ReorderLevel, ct);

        var needsAttention = await products
            .Where(p => p.currentStock <= p.ReorderLevel)
            .OrderBy(p => p.currentStock)
            .Take(5)
            .Select(p => new ProductRowViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                CurrentStock = p.currentStock,
                ReorderLevel = p.ReorderLevel,
                TargetStockLevel = p.TargetStockLevel
            })
            .ToListAsync(ct);

        var openAlerts = await _db.InventoryAlerts
            .AsNoTracking()
            .Where(a => !a.IsResolved)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Take(5)
            .Select(a => new AlertRowViewModel
            {
                Id = a.Id,
                ProductName = a.Product.Name,
                Message = a.Message,
                Severity = a.Severity,
                CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync(ct);

        var recentActivity = await _db.StockTransactions
            .AsNoTracking()
            .OrderByDescending(t => t.TimestampUtc)
            .Take(6)
            .Select(t => new ActivityRowViewModel
            {
                ProductName = t.Product.Name,
                Type = t.Type,
                Quantity = t.Quantity,
                ReferenceNumber = t.ReferenceNumber,
                Notes = t.Notes,
                PerformedBy = t.PerformedBy,
                TimestampUtc = t.TimestampUtc
            })
            .ToListAsync(ct);

        return View(new DashboardViewModel
        {
            TotalProducts = totalProducts,
            TotalUnits = totalUnits,
            InventoryValue = inventoryValue,
            LowStockCount = lowStockCount,
            NeedsAttention = needsAttention,
            OpenAlerts = openAlerts,
            RecentActivity = recentActivity
        });
    }
}
