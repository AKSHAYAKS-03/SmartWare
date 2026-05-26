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

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("Creator User ID is required.");

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

        RuleFor(x => x.ReceivedBy)
            .NotEmpty().WithMessage("Receiver User ID is required.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Goods Receipt must contain at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PurchaseOrderItemId)
                .NotEmpty().WithMessage("Purchase Order Item ID is required.");

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

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Requester User ID is required.");

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
