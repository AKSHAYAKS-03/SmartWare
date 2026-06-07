using FluentValidation;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Validators;

public class PurchaseOrderCreateValidator : AbstractValidator<PurchaseOrderCreateDto>
{
    public PurchaseOrderCreateValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier ID is required.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.ExpectedDelivery)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expected delivery date must be in the future.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddYears(1)).WithMessage("Expected delivery date cannot be more than 1 year in the future.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Purchase Order must contain at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("Product ID is required for item.");

            item.RuleFor(i => i.QuantityOrdered)
                .GreaterThan(0).WithMessage("Quantity Ordered must be greater than 0.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThan(0).WithMessage("Unit Price must be greater than 0.");
        });
    }
}

public class GoodsReceiptCreateValidator : AbstractValidator<GoodsReceiptCreateDto>
{
    public GoodsReceiptCreateValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty().WithMessage("Purchase Order ID is required.");


        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Goods Receipt must contain at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PurchaseOrderItemId)
                .NotEmpty().WithMessage("Purchase Order Item ID is required.");

            item.RuleFor(i => i.BinLocationId)
                .NotEmpty().WithMessage(
                    "Bin Location is required for every received item. " +
                    "Stock must be physically placed in a specific bin. " +
                    "Please ensure at least one active bin exists in this warehouse before receiving goods.");

            item.RuleFor(i => i.QuantityReceived)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity Received must be 0 or greater.");

            item.RuleFor(i => i.QuantityRejected)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity Rejected must be 0 or greater.");
        });
    }
}

public class TransferCreateValidator : AbstractValidator<TransferCreateDto>
{
    public TransferCreateValidator()
    {
        RuleFor(x => x.FromWarehouseId)
            .NotEmpty().WithMessage("Source warehouse is required.");

        RuleFor(x => x.ToWarehouseId)
            .NotEmpty().WithMessage("Destination warehouse is required.")
            .NotEqual(x => x.FromWarehouseId).WithMessage("Source and destination warehouses cannot be the same.");


        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Transfer must contain at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("Product ID is required for item.");

            item.RuleFor(i => i.QuantityRequested)
                .GreaterThan(0).WithMessage("Quantity Requested must be greater than 0.");
        });
    }
}
