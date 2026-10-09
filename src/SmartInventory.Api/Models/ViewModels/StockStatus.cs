namespace SmartInventory.Api.Models.ViewModels;

/// <summary>Derived stock health of a product. Single source of truth for the rules.</summary>
public enum StockStatus
{
    Healthy,
    Low,
    Out
}

public static class StockStatusRules
{
    public static StockStatus From(int currentStock, int reorderLevel) =>
        currentStock <= 0 ? StockStatus.Out
        : currentStock <= reorderLevel ? StockStatus.Low
        : StockStatus.Healthy;
}
