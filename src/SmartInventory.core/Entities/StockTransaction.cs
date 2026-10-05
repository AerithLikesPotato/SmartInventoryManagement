namespace SmartInventory.Core.Entities;

public enum TransactionType
{
    StockIn,
    StockOut,
    Adjustment,
    Return
}
public class StockTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public  Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public TransactionType Type { get; set; }
    public int Quantity { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
