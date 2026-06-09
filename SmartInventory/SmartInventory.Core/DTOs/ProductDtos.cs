using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region Category DTOs
public class CategoryCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class CategoryResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<CategoryTreeDto> SubCategories { get; set; } = [];
}
#endregion

#region Product DTOs
public class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImagePath { get; set; }

    // Dimensions
    public decimal Length { get; set; } = 0;
    public decimal Width { get; set; } = 0;
    public decimal Height { get; set; } = 0;
    public decimal WeightKg { get; set; } = 0;
}

public class ProductUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; }
    public string? ImagePath { get; set; }

    // Dimensions
    public decimal Length { get; set; } = 0;
    public decimal Width { get; set; } = 0;
    public decimal Height { get; set; } = 0;
    public decimal WeightKg { get; set; } = 0;
}

public class ProductResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public string UnitOfMeasureName => UnitOfMeasure.ToString();
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public bool IsActive { get; set; }
    public string? ImagePath { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Dimensions
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal WeightKg { get; set; }
    public decimal VolumeCm3 { get; set; }
}

public class ProductSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
}

public class ProductQueryParameters : QueryParameters
{
    public Guid? CategoryId { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? IsActive { get; set; }
}
#endregion

#region ProductVariant DTOs
public class ProductVariantCreateDto
{
    public Guid ProductId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public string? Attributes { get; set; } // JSON string
    public bool IsActive { get; set; } = true;
}

public class ProductVariantUpdateDto
{
    public string VariantName { get; set; } = string.Empty;
    public string? Attributes { get; set; }
    public bool IsActive { get; set; }
}

public class ProductVariantResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string? Attributes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion

#region AlertConfiguration DTOs
public class AlertConfigCreateDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public int LowStockThreshold { get; set; }
    public bool SmsAlert { get; set; }
    public bool EmailAlert { get; set; }
    public bool InAppAlert { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AlertConfigResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int LowStockThreshold { get; set; }
    public bool SmsAlert { get; set; }
    public bool EmailAlert { get; set; }
    public bool InAppAlert { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion
