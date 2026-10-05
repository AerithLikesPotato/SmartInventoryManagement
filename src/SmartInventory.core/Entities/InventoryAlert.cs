namespace SmartInventory.Core.Entities;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
public class InventoryAlert
{
    public Guid Id { get; set;} = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null;

    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; } 
    public bool IsResolved {get; set;} = false;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}