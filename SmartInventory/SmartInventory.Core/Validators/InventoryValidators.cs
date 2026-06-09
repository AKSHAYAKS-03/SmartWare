using FluentValidation;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Validators;

public class ProductCreateValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product Name is required.")
            .Length(3, 200).WithMessage("Product Name must be between 3 and 200 characters.");

        RuleFor(x => x.CostPrice)
            .GreaterThan(0).WithMessage("Cost Price must be greater than 0.");

        RuleFor(x => x.SellingPrice)
            .GreaterThanOrEqualTo(x => x.CostPrice).WithMessage("Selling Price must be greater than or equal to Cost Price.");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder Point must be greater than or equal to 0.");

        RuleFor(x => x.ReorderQuantity)
            .GreaterThan(0).WithMessage("Reorder Quantity must be greater than 0.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category ID is required.");
    }
}

public class ProductUpdateValidator : AbstractValidator<ProductUpdateDto>
{
    public ProductUpdateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product Name is required.")
            .Length(3, 200).WithMessage("Product Name must be between 3 and 200 characters.");

        RuleFor(x => x.CostPrice)
            .GreaterThan(0).WithMessage("Cost Price must be greater than 0.");

        RuleFor(x => x.SellingPrice)
            .GreaterThanOrEqualTo(x => x.CostPrice).WithMessage("Selling Price must be greater than or equal to Cost Price.");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder Point must be greater than or equal to 0.");

        RuleFor(x => x.ReorderQuantity)
            .GreaterThan(0).WithMessage("Reorder Quantity must be greater than 0.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category ID is required.");
    }
}

public class CategoryCreateValidator : AbstractValidator<CategoryCreateDto>
{
    public CategoryCreateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category Name is required.")
            .Length(2, 100).WithMessage("Category Name must be between 2 and 100 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
    }
}

public class CategoryUpdateValidator : AbstractValidator<CategoryUpdateDto>
{
    public CategoryUpdateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category Name is required.")
            .Length(2, 100).WithMessage("Category Name must be between 2 and 100 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
    }
}

public class SupplierCreateValidator : AbstractValidator<SupplierCreateDto>
{
    public SupplierCreateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier Name is required.")
            .Length(2, 150).WithMessage("Supplier Name must be between 2 and 150 characters.");

        RuleFor(x => x.GSTIN)
            .NotEmpty().WithMessage("GSTIN is required.")
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
            .WithMessage("GSTIN must be a valid 15-character Indian format.");

        RuleFor(x => x.PAN)
            .NotEmpty().WithMessage("PAN is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$")
            .WithMessage("PAN must be a valid 10-character Indian format (e.g., ABCDE1234F).");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.LeadTimeDays)
            .GreaterThanOrEqualTo(0).WithMessage("Lead Time Days must be 0 or greater.");

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Credit Limit must be 0 or greater.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^\+91[6-9]\d{9}$").WithMessage("Phone number must be a valid Indian phone number (e.g. +919876543210).");
    }
}

public class SupplierUpdateValidator : AbstractValidator<SupplierUpdateDto>
{
    public SupplierUpdateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier Name is required.")
            .Length(2, 150).WithMessage("Supplier Name must be between 2 and 150 characters.");

        RuleFor(x => x.GSTIN)
            .NotEmpty().WithMessage("GSTIN is required.")
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
            .WithMessage("GSTIN must be a valid 15-character Indian format.");

        RuleFor(x => x.PAN)
            .NotEmpty().WithMessage("PAN is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$")
            .WithMessage("PAN must be a valid 10-character Indian format (e.g., ABCDE1234F).");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.LeadTimeDays)
            .GreaterThanOrEqualTo(0).WithMessage("Lead Time Days must be 0 or greater.");

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Credit Limit must be 0 or greater.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^\+91[6-9]\d{9}$").WithMessage("Phone number must be a valid Indian phone number (e.g. +919876543210).");
    }
}

public class AlertConfigCreateValidator : AbstractValidator<AlertConfigCreateDto>
{
    public AlertConfigCreateValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.LowStockThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Low Stock Threshold must be 0 or greater.");

        RuleFor(x => x)
            .Must(x => x.SmsAlert || x.EmailAlert || x.InAppAlert)
            .WithMessage("At least one notification channel (SMS, Email, or In-App) must be enabled.");
    }
}

public class StockAdjustmentCreateValidator : AbstractValidator<StockAdjustmentCreateDto>
{
    public StockAdjustmentCreateValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.QuantityBefore)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity Before must be 0 or greater.");

        RuleFor(x => x.QuantityAfter)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity After must be 0 or greater.");

        RuleFor(x => x)
            .Must(x => x.QuantityBefore != x.QuantityAfter)
            .WithMessage("Quantity Before and Quantity After cannot be identical.");
    }
}
