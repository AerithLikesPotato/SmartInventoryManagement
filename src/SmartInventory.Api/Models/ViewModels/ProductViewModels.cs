using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Api.Models.ViewModels;

public class ProductRowViewModel
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public string? ImageUrl { get; init; }
    public int CurrentStock { get; init; }
    public int ReorderLevel { get; init; }
    public int TargetStockLevel { get; init; }

    public StockStatus Status => StockStatusRules.From(CurrentStock, ReorderLevel);

    /// <summary>Upper bound of the stock meter (never 0, never below current stock).</summary>
    public int MeterMax => Math.Max(1, Math.Max(TargetStockLevel, CurrentStock));

    public int FillPercent => Math.Clamp((int)Math.Round(CurrentStock * 100.0 / MeterMax), 0, 100);
}

public enum StockFilter
{
    All,
    Healthy,
    Low,
    Out
}

public class ProductListViewModel
{
    public IReadOnlyList<ProductRowViewModel> Items { get; init; } = [];
    public string? Query { get; init; }
    public StockFilter Filter { get; init; } = StockFilter.All;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Query) || Filter != StockFilter.All;
}

public class ProductFormViewModel : IValidatableObject
{
    [Required(ErrorMessage = "SKU is required")]
    [StringLength(100, ErrorMessage = "SKU can be at most 100 characters")]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(200, ErrorMessage = "Name can be at most 200 characters")]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description can be at most 1000 characters")]
    public string? Description { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a price between 0 and 1,000,000")]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Initial stock")]
    public int InitialStock { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Target stock level")]
    public int TargetStockLevel { get; set; }

    [Display(Name = "Product image")]
    public IFormFile? ImageFile { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TargetStockLevel < ReorderLevel)
        {
            yield return new ValidationResult(
                "Target level must be at least the reorder level",
                [nameof(TargetStockLevel)]);
        }
    }

    /// <summary>Maps to the Core contract so the controller depends on it, not on form fields.</summary>
    public CreateProductDto ToDto() => new(
        Sku.Trim(),
        Name.Trim(),
        string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        UnitPrice,
        InitialStock,
        ReorderLevel,
        TargetStockLevel);
}

public class ProductEditViewModel : IValidatableObject
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "SKU is required")]
    [StringLength(100, ErrorMessage = "SKU can be at most 100 characters")]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(200, ErrorMessage = "Name can be at most 200 characters")]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description can be at most 1000 characters")]
    public string? Description { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a price between 0 and 1,000,000")]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Current stock")]
    public int CurrentStock { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a quantity of 0 or more")]
    [Display(Name = "Target stock level")]
    public int TargetStockLevel { get; set; }

    public string? ExistingImageUrl { get; set; }

    [Display(Name = "Product image")]
    public IFormFile? ImageFile { get; set; }

    [Display(Name = "Remove current picture")]
    public bool RemoveImage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TargetStockLevel < ReorderLevel)
        {
            yield return new ValidationResult(
                "Target level must be at least the reorder level",
                [nameof(TargetStockLevel)]);
        }
    }
}
