using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Api.Models.ViewModels;
using SmartInventory.Core.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Api.Controllers;

[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private const int PageSize = 10;
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private readonly InventoryDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProductsController(InventoryDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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
                ImageUrl = p.ImageUrl,
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

        string? uploadedImageUrl = null;
        if (model.ImageFile is { Length: > 0 })
        {
            var (url, err) = await ProcessImageUploadAsync(model.ImageFile, ct);
            if (err != null)
            {
                ModelState.AddModelError(nameof(model.ImageFile), err);
            }
            else
            {
                uploadedImageUrl = url;
            }
        }

        if (!ModelState.IsValid)
        {
            if (uploadedImageUrl != null)
            {
                TryDeleteProductImage(uploadedImageUrl);
            }
            return View(model);
        }

        var product = new Product
        {
            Sku = dto.Sku,
            Name = dto.Name,
            Description = dto.Desciption,
            UnitPrice = dto.UnitPrice,
            ImageUrl = uploadedImageUrl,
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
                Notes = "Initial stock",
                PerformedBy = User.Identity?.Name ?? "System"
            });
        }

        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (uploadedImageUrl != null)
            {
                TryDeleteProductImage(uploadedImageUrl);
            }
            ModelState.AddModelError(nameof(model.Sku), "A product with this SKU already exists");
            return View(model);
        }

        TempData["Success"] = $"“{product.Name}” was added to your inventory.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null)
        {
            return NotFound();
        }

        var model = new ProductEditViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            UnitPrice = product.UnitPrice,
            CurrentStock = product.currentStock,
            ReorderLevel = product.ReorderLevel,
            TargetStockLevel = product.TargetStockLevel,
            ExistingImageUrl = product.ImageUrl
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductEditViewModel model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null)
        {
            return NotFound();
        }

        var normalizedSku = model.Sku.Trim();
        if (await _db.Products.AnyAsync(p => p.Sku == normalizedSku && p.Id != id, ct))
        {
            ModelState.AddModelError(nameof(model.Sku), "A product with this SKU already exists");
        }

        string? newUploadedUrl = null;
        if (model.ImageFile is { Length: > 0 })
        {
            var (url, err) = await ProcessImageUploadAsync(model.ImageFile, ct);
            if (err != null)
            {
                ModelState.AddModelError(nameof(model.ImageFile), err);
            }
            else
            {
                newUploadedUrl = url;
            }
        }

        if (!ModelState.IsValid)
        {
            if (newUploadedUrl != null)
            {
                TryDeleteProductImage(newUploadedUrl);
            }
            model.ExistingImageUrl = product.ImageUrl;
            return View(model);
        }

        // Handle image update or removal
        if (newUploadedUrl != null)
        {
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                TryDeleteProductImage(product.ImageUrl);
            }
            product.ImageUrl = newUploadedUrl;
        }
        else if (model.RemoveImage)
        {
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                TryDeleteProductImage(product.ImageUrl);
            }
            product.ImageUrl = null;
        }

        // Record stock adjustments if stock was changed
        var stockDelta = model.CurrentStock - product.currentStock;
        if (stockDelta != 0)
        {
            product.currentStock = model.CurrentStock;
            _db.StockTransactions.Add(new StockTransaction
            {
                ProductId = product.Id,
                Type = TransactionType.Adjustment,
                Quantity = stockDelta,
                Notes = $"Stock adjustment during product edit ({stockDelta:+0;-0})",
                PerformedBy = User.Identity?.Name ?? "System"
            });
        }

        product.Sku = normalizedSku;
        product.Name = model.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        product.UnitPrice = model.UnitPrice;
        product.ReorderLevel = model.ReorderLevel;
        product.TargetStockLevel = model.TargetStockLevel;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (newUploadedUrl != null)
            {
                TryDeleteProductImage(newUploadedUrl);
            }

            if (ex is DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "The product was modified by another process. Please refresh and try again.");
            }
            else
            {
                ModelState.AddModelError(nameof(model.Sku), "A product with this SKU already exists");
            }

            model.ExistingImageUrl = product.ImageUrl;
            return View(model);
        }

        TempData["Success"] = $"“{product.Name}” was updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<(string? Url, string? Error)> ProcessImageUploadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return (null, null);
        }

        if (file.Length > MaxImageSizeBytes)
        {
            return (null, "Image must be 5 MB or smaller.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            return (null, "Only .jpg, .jpeg, .png, .webp, and .gif images are supported.");
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "products");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, uniqueFileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return ($"/uploads/products/{uniqueFileName}", null);
    }

    private void TryDeleteProductImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        if (!imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch
        {
            // Ignore non-fatal file deletion errors
        }
    }
}
