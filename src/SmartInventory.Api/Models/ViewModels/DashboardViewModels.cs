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
    public DateTime TimestampUtc { get; init; }
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
