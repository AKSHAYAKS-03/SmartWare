using FluentValidation;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Validators;

public class WarehouseCreateValidator : AbstractValidator<WarehouseCreateDto>
{
    public WarehouseCreateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse Name is required.")
            .Length(2, 100).WithMessage("Warehouse Name must be between 2 and 100 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Warehouse Code is required.")
            .Matches(@"^[A-Z0-9-]{3,10}$").WithMessage("Warehouse Code must be 3-10 characters long, containing uppercase letters, numbers, or hyphens.");

        RuleFor(x => x.TaxIdentifier)
            .NotEmpty().WithMessage("Tax Identifier (e.g. GSTIN, VAT ID) is required for compliance validation.")
            .Matches(@"^[A-Z0-9]{8,15}$").WithMessage("Tax Identifier must be 8-15 characters long and alphanumeric.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Official government business registration/permit code is required.")
            .Matches(@"^[A-Za-z0-9-/]{5,30}$").WithMessage("Registration number must be 5-30 characters long and alphanumeric.");
    }
}

public class WarehouseUpdateValidator : AbstractValidator<WarehouseUpdateDto>
{
    public WarehouseUpdateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse Name is required.")
            .Length(2, 100).WithMessage("Warehouse Name must be between 2 and 100 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Warehouse Code is required.")
            .Matches(@"^[A-Z0-9-]{3,10}$").WithMessage("Warehouse Code must be 3-10 characters long, containing uppercase letters, numbers, or hyphens.");

        RuleFor(x => x.TaxIdentifier)
            .NotEmpty().WithMessage("Tax Identifier (e.g. GSTIN, VAT ID) is required for compliance validation.")
            .Matches(@"^[A-Z0-9]{8,15}$").WithMessage("Tax Identifier must be 8-15 characters long and alphanumeric.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Official government business registration/permit code is required.")
            .Matches(@"^[A-Za-z0-9-/]{5,30}$").WithMessage("Registration number must be 5-30 characters long and alphanumeric.");
    }
}

public class ZoneCreateValidator : AbstractValidator<ZoneCreateDto>
{
    public ZoneCreateValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Warehouse ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Zone Name is required.")
            .Length(2, 50).WithMessage("Zone Name must be between 2 and 50 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Zone Code is required.")
            .Matches(@"^[A-Z0-9-]{2,10}$").WithMessage("Zone Code must be 2-10 characters long, containing uppercase letters, numbers, or hyphens.");
    }
}

public class ZoneUpdateValidator : AbstractValidator<ZoneUpdateDto>
{
    public ZoneUpdateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Zone Name is required.")
            .Length(2, 50).WithMessage("Zone Name must be between 2 and 50 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Zone Code is required.")
            .Matches(@"^[A-Z0-9-]{2,10}$").WithMessage("Zone Code must be 2-10 characters long, containing uppercase letters, numbers, or hyphens.");
    }
}

public class BinLocationCreateValidator : AbstractValidator<BinLocationCreateDto>
{
    public BinLocationCreateValidator()
    {
        RuleFor(x => x.ZoneId)
            .NotEmpty().WithMessage("Zone ID is required.");

        RuleFor(x => x.Aisle)
            .NotEmpty().WithMessage("Aisle identifier is required.")
            .Length(1, 10).WithMessage("Aisle identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Rack)
            .NotEmpty().WithMessage("Rack identifier is required.")
            .Length(1, 10).WithMessage("Rack identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Bin)
            .NotEmpty().WithMessage("Bin identifier is required.")
            .Length(1, 10).WithMessage("Bin identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Barcode)
            .Matches(@"^[A-Za-z0-9-]{3,30}$").When(x => !string.IsNullOrEmpty(x.Barcode))
            .WithMessage("Bin Barcode must be between 3 and 30 characters and alphanumeric.");
    }
}

public class BinLocationUpdateValidator : AbstractValidator<BinLocationUpdateDto>
{
    public BinLocationUpdateValidator()
    {
        RuleFor(x => x.Aisle)
            .NotEmpty().WithMessage("Aisle identifier is required.")
            .Length(1, 10).WithMessage("Aisle identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Rack)
            .NotEmpty().WithMessage("Rack identifier is required.")
            .Length(1, 10).WithMessage("Rack identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Bin)
            .NotEmpty().WithMessage("Bin identifier is required.")
            .Length(1, 10).WithMessage("Bin identifier must be between 1 and 10 characters.");

        RuleFor(x => x.Barcode)
            .Matches(@"^[A-Za-z0-9-]{3,30}$").When(x => !string.IsNullOrEmpty(x.Barcode))
            .WithMessage("Bin Barcode must be between 3 and 30 characters and alphanumeric.");
    }
}

public class QueryParametersValidator : AbstractValidator<QueryParameters>
{
    public QueryParametersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortDir)
            .Must(x => x == "asc" || x == "desc" || string.IsNullOrEmpty(x))
            .WithMessage("Sort direction must be either 'asc' or 'desc'.");
    }
}
