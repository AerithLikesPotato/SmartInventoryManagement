using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Api.Models.ViewModels;
using SmartInventory.Core.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Api.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private const int PageSize = 10;

    private readonly InventoryDbContext _db;

    public ProductsController(InventoryDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        StockFilter filter = StockFilter.All,
        int page = 1,
        CancellationToken ct = default)
    {
        IQueryable<Product> query = _db.Products.AsNoTracking();

        var term = q?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = term.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(lowered) || p.Sku.ToLower().Contains(lowered));
        }

        // Keep these predicates in sync with StockStatusRules.From.
        query = filter switch
        {
            StockFilter.Out => query.Where(p => p.currentStock <= 0),
            StockFilter.Low => query.Where(p => p.currentStock > 0 && p.currentStock <= p.ReorderLevel),
            StockFilter.Healthy => query.Where(p => p.currentStock > p.ReorderLevel),
            _ => query
        };

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
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

        return View(new ProductListViewModel
        {
            Items = items,
            Query = term,
            Filter = filter,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new ProductFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        var dto = model.ToDto();

        if (await _db.Products.AnyAsync(p => p.Sku == dto.Sku, ct))
        {
            ModelState.AddModelError(nameof(model.Sku), "A product with this SKU already exists");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = new Product
        {
            Sku = dto.Sku,
            Name = dto.Name,
            Description = dto.Desciption,
            UnitPrice = dto.UnitPrice,
            currentStock = dto.InitialStock,
            ReorderLevel = dto.ReorderLevel,
            TargetStockLevel = dto.TargetStockLevel
        };

        if (dto.InitialStock > 0)
        {
            product.Transactions.Add(new StockTransaction
            {
                Type = TransactionType.StockIn,
                Quantity = dto.InitialStock,
                Notes = "Initial stock"
            });
        }

        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against the unique SKU index.
            ModelState.AddModelError(nameof(model.Sku), "A product with this SKU already exists");
            return View(model);
        }

        TempData["Success"] = $"“{product.Name}” was added to your inventory.";
        return RedirectToAction(nameof(Index));
    }
}
