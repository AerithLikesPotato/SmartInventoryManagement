namespace SmartInventory.Core.DTOs;

public record CreateProductDto(
    string Sku,
    string Name,
    string? Desciption,
    decimal UnitPrice,
    int InitialStock,
    int ReorderLevel,
    int TargetStockLevel
    );