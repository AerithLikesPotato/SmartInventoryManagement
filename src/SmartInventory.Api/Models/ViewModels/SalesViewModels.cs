namespace SmartInventory.Api.Models.ViewModels;

/// <summary>
/// What a cashier is allowed to see about a product: identity, selling price, units on hand.
/// Deliberately has NO cost, margin, reorder level or target level.
/// </summary>
public class PosProductViewModel
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public string? ImageUrl { get; init; }
    public int CurrentStock { get; init; }

    /// <summary>"out" | "low" | "ok" — drives the tile badge. Computed server-side so the reorder level never leaves it.</summary>
    public string StockState { get; init; } = "ok";

    /// <summary>Lower-cased "name sku" for the instant client-side filter.</summary>
    public string SearchText => (Name + " " + Sku).ToLowerInvariant();
}

public class SalesIndexViewModel
{
    public IReadOnlyList<PosProductViewModel> Products { get; init; } = [];

    /// <summary>ISO 4217 code (USD, EUR, THB…). From configuration key "Pos:Currency".</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>True when the catalogue was cut at the display limit and the cashier should be told.</summary>
    public bool IsTruncated { get; init; }
}
