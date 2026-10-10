namespace SmartInventory.Core.Entities;

public class Product
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    
    //inventory
    public int currentStock { get; set; }
    public int ReorderLevel { get; set; }
    public int TargetStockLevel { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } =  DateTime.UtcNow;

    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}