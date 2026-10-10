using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Api.Models.ViewModels;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Api.Controllers;

[Authorize(Roles = "Cashier,Admin")]
public class SalesController : Controller
{
    /// <summary>Upper bound for the POS grid. Beyond this, switch the grid to a server-side search endpoint.</summary>
    private const int CatalogLimit = 500;

    private readonly InventoryDbContext _db;
    private readonly ISalesService _sales;
    private readonly IConfiguration _config;

    public SalesController(InventoryDbContext db, ISalesService sales, IConfiguration config)
    {
        _db = db;
        _sales = sales;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Fetch one extra row so we know whether the list was cut off.
        var rows = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Take(CatalogLimit + 1)
            .Select(p => new PosProductViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                ImageUrl = p.ImageUrl,
                CurrentStock = p.currentStock,
                // Translated to SQL; the reorder level itself is never selected into the view model.
                StockState = p.currentStock <= 0 ? "out"
                           : p.currentStock <= p.ReorderLevel ? "low"
                           : "ok"
            })
            .ToListAsync(ct);

        return View(new SalesIndexViewModel
        {
            Products = rows.Take(CatalogLimit).ToList(),
            IsTruncated = rows.Count > CatalogLimit,
            Currency = _config["Pos:Currency"] ?? "USD"
        });
    }

    /// <summary>
    /// JSON endpoint called by js/modules/pos. Antiforgery token travels in the
    /// "RequestVerificationToken" header (the framework's default header name).
    /// 200 = sold · 409 = not enough stock (body lists what is available) or duplicate sale · 400 = bad request.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto? request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(CheckoutResultDto.Failed(
                new CheckoutErrorDto(CheckoutErrorCodes.Invalid, "The request was empty or malformed.")));
        }

        var cashier = User.Identity?.Name ?? "unknown";
        var result = await _sales.CheckoutAsync(request, cashier, ct);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Errors.Any(e => e.Code is CheckoutErrorCodes.InsufficientStock or CheckoutErrorCodes.Duplicate)
            ? Conflict(result)
            : BadRequest(result);
    }
}
