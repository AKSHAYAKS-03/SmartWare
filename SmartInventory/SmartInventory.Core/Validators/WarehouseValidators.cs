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

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required for Indian compliance and reporting.");

        RuleFor(x => x.PostalCode)
            .Matches(@"^[1-9][0-9]{5}$").When(x => !string.IsNullOrEmpty(x.PostalCode))
            .WithMessage("Postal code must be a valid 6-digit Indian PIN code.");

        RuleFor(x => x.GSTIN)
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$").When(x => !string.IsNullOrEmpty(x.GSTIN))
            .WithMessage("Invalid Indian GSTIN format.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Invalid email address format.");

        RuleFor(x => x.RegistrationNumber)
            .Matches(@"^[A-Za-z0-9-/]{5,30}$").When(x => !string.IsNullOrEmpty(x.RegistrationNumber))
            .WithMessage("Registration number must be 5-30 characters long and alphanumeric.");

        RuleFor(x => x.AreaSqFt).GreaterThan(0).WithMessage("Warehouse Area must be greater than 0. A warehouse cannot be created without a defined capacity.");
        RuleFor(x => x.MaxVolumeCm3).GreaterThan(0).WithMessage("Warehouse Volume must be greater than 0. A warehouse cannot be created without a defined capacity.");
        RuleFor(x => x.MaxWeightKg).GreaterThan(0).WithMessage("Warehouse Weight must be greater than 0. A warehouse cannot be created without a defined capacity.");
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

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required for Indian compliance and reporting.");

        RuleFor(x => x.PostalCode)
            .Matches(@"^[1-9][0-9]{5}$").When(x => !string.IsNullOrEmpty(x.PostalCode))
            .WithMessage("Postal code must be a valid 6-digit Indian PIN code.");

        RuleFor(x => x.GSTIN)
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$").When(x => !string.IsNullOrEmpty(x.GSTIN))
            .WithMessage("Invalid Indian GSTIN format.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Invalid email address format.");

        RuleFor(x => x.RegistrationNumber)
            .Matches(@"^[A-Za-z0-9-/]{5,30}$").When(x => !string.IsNullOrEmpty(x.RegistrationNumber))
            .WithMessage("Registration number must be 5-30 characters long and alphanumeric.");

        RuleFor(x => x.AreaSqFt).GreaterThan(0).WithMessage("Warehouse Area must be greater than 0. A warehouse cannot be created without a defined capacity.");
        RuleFor(x => x.MaxVolumeCm3).GreaterThan(0).WithMessage("Warehouse Volume must be greater than 0. A warehouse cannot be created without a defined capacity.");
        RuleFor(x => x.MaxWeightKg).GreaterThan(0).WithMessage("Warehouse Weight must be greater than 0. A warehouse cannot be created without a defined capacity.");
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

        RuleFor(x => x.AreaSqFt).GreaterThan(0).WithMessage("Zone Area must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
        RuleFor(x => x.MaxVolumeCm3).GreaterThan(0).WithMessage("Zone Volume must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
        RuleFor(x => x.MaxWeightKg).GreaterThan(0).WithMessage("Zone Weight must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
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

        RuleFor(x => x.AreaSqFt).GreaterThan(0).WithMessage("Zone Area must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
        RuleFor(x => x.MaxVolumeCm3).GreaterThan(0).WithMessage("Zone Volume must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
        RuleFor(x => x.MaxWeightKg).GreaterThan(0).WithMessage("Zone Weight must be greater than 0. A Zone must have a defined capacity allocation from its parent Warehouse.");
    }
}

public class BinLocationCreateValidator : AbstractValidator<BinLocationCreateDto>
{
    public BinLocationCreateValidator()
    {
        RuleFor(x => x.ZoneId)
            .NotEmpty().WithMessage("Zone ID is required.");

        RuleFor(x => x.BinCode)
            .NotEmpty().WithMessage("BinCode is required.")
            .Length(1, 30).WithMessage("BinCode must be between 1 and 30 characters.")
            .Matches(@"^[A-Z0-9][A-Z0-9\-_]{0,29}$")
            .WithMessage("BinCode must start with an uppercase letter or number and contain only uppercase letters, numbers, hyphens, or underscores (e.g. B01, SHELF-1, BIN_A).");

        RuleFor(x => x.Barcode)
            .Matches(@"^[A-Za-z0-9-]{3,30}$").When(x => !string.IsNullOrEmpty(x.Barcode))
            .WithMessage("Bin Barcode must be between 3 and 30 characters and alphanumeric.");

        RuleFor(x => x.MaxVolumeCm3)
            .GreaterThan(0)
            .WithMessage("Bin Volume must be greater than 0. Unlimited bins are not supported in this system.");

        RuleFor(x => x.MaxWeightKg)
            .GreaterThan(0)
            .WithMessage("Bin Weight must be greater than 0. Unlimited bins are not supported in this system.");
    }
}

public class BinLocationUpdateValidator : AbstractValidator<BinLocationUpdateDto>
{
    public BinLocationUpdateValidator()
    {
        RuleFor(x => x.BinCode)
            .NotEmpty().WithMessage("BinCode is required.")
            .Length(1, 30).WithMessage("BinCode must be between 1 and 30 characters.")
            .Matches(@"^[A-Z0-9][A-Z0-9\-_]{0,29}$")
            .WithMessage("BinCode must start with an uppercase letter or number and contain only uppercase letters, numbers, hyphens, or underscores.");

        RuleFor(x => x.Barcode)
            .Matches(@"^[A-Za-z0-9-]{3,30}$").When(x => !string.IsNullOrEmpty(x.Barcode))
            .WithMessage("Bin Barcode must be between 3 and 30 characters and alphanumeric.");

        RuleFor(x => x.MaxVolumeCm3)
            .GreaterThan(0)
            .WithMessage("Bin Volume must be greater than 0. Unlimited bins are not supported in this system.");

        RuleFor(x => x.MaxWeightKg)
            .GreaterThan(0)
            .WithMessage("Bin Weight must be greater than 0. Unlimited bins are not supported in this system.");
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
