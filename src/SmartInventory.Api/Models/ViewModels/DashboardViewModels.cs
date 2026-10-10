using SmartInventory.Core.Entities;

namespace SmartInventory.Api.Models.ViewModels;

public class AlertRowViewModel
{
    public Guid Id { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public AlertSeverity Severity { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class ActivityRowViewModel
{
    public string ProductName { get; init; } = string.Empty;
    public TransactionType Type { get; init; }
    public int Quantity { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public string? PerformedBy { get; init; }
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Displays the user/cashier who performed the action, falling back to parsed notes or "System".
    /// </summary>
    public string Actor
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PerformedBy))
                return PerformedBy;

            if (Notes != null && Notes.StartsWith("POS sale · ", StringComparison.OrdinalIgnoreCase))
                return Notes["POS sale · ".Length..].Trim();

            return "System";
        }
    }
}

public class DashboardViewModel
{
    public int TotalProducts { get; init; }
    public int TotalUnits { get; init; }
    public decimal InventoryValue { get; init; }
    public int LowStockCount { get; init; }

    public IReadOnlyList<ProductRowViewModel> NeedsAttention { get; init; } = [];
    public IReadOnlyList<AlertRowViewModel> OpenAlerts { get; init; } = [];
    public IReadOnlyList<ActivityRowViewModel> RecentActivity { get; init; } = [];
}
