using SmartInventory.Core.Entities;

namespace SmartInventory.Api.Models.ViewModels;

/// <summary>
/// Maps domain enums to display text and a "tone" (success | warning | danger | info).
/// The tone is the CSS modifier shared by .badge--*, .meter--* and .stat--*,
/// so views never contain colour decisions.
/// </summary>
public static class PresentationExtensions
{
    public static string Label(this StockStatus status) => status switch
    {
        StockStatus.Out => "Out of stock",
        StockStatus.Low => "Low stock",
        _ => "In stock"
    };

    public static string Tone(this StockStatus status) => status switch
    {
        StockStatus.Out => "danger",
        StockStatus.Low => "warning",
        _ => "success"
    };

    public static string Label(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "Critical",
        AlertSeverity.Warning => "Warning",
        _ => "Info"
    };

    public static string Tone(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "danger",
        AlertSeverity.Warning => "warning",
        _ => "info"
    };

    public static string Label(this TransactionType type) => type switch
    {
        TransactionType.StockIn => "Stock in",
        TransactionType.StockOut => "Stock out",
        TransactionType.Return => "Return",
        _ => "Adjustment"
    };

    /// <summary>Stock-in and returns add units; stock-out removes them.</summary>
    public static string Sign(this TransactionType type) => type switch
    {
        TransactionType.StockIn or TransactionType.Return => "+",
        TransactionType.StockOut => "−",
        _ => "±"
    };
}
