using FluentValidation;
using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Validators;

// ──────────────────────────────────────────────────────────────────────────────
// ONBOARDING VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Admin invite — validates GSTIN/email/phone format before hitting the DB.</summary>
public class SupplierInviteValidator : AbstractValidator<SupplierInviteRequest>
{
    public SupplierInviteValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier name is required.")
            .Length(2, 150).WithMessage("Supplier name must be between 2 and 150 characters.");

        RuleFor(x => x.GSTIN)
            .NotEmpty().WithMessage("GSTIN is required.")
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
            .WithMessage("GSTIN must be a valid 15-character Indian format (e.g. 27AABCT1234C1Z5).");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Business email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Business phone is required.")
            .Matches(@"^\+91[6-9]\d{9}$")
            .WithMessage("Phone must be a valid Indian mobile number (e.g. +919876543210).");
    }
}

/// <summary>Supplier self-registration — full validation of company and contact details.</summary>
public class SupplierRegisterValidator : AbstractValidator<SupplierRegisterRequest>
{
    public SupplierRegisterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .Length(2, 150).WithMessage("Company name must be between 2 and 150 characters.");

        RuleFor(x => x.GSTIN)
            .NotEmpty().WithMessage("GSTIN is required.")
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
            .WithMessage("GSTIN must be a valid 15-character Indian format (e.g. 27AABCM1234C1Z5).");

        RuleFor(x => x.PAN)
            .NotEmpty().WithMessage("PAN is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$")
            .WithMessage("PAN must be a valid 10-character Indian format (e.g. ABCDE1234F).");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.");

        RuleFor(x => x.ContactFullName)
            .NotEmpty().WithMessage("Contact full name is required.")
            .Length(2, 150).WithMessage("Contact full name must be between 2 and 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .Matches(@"^\+91[6-9]\d{9}$")
            .WithMessage("Phone must be a valid Indian mobile number (e.g. +919876543210).");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

/// <summary>OTP email verification — ensures email and token are present.</summary>
public class SupplierVerifyEmailValidator : AbstractValidator<SupplierVerifyEmailRequest>
{
    public SupplierVerifyEmailValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("OTP token is required.")
            .Length(6, 6).WithMessage("OTP token must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("OTP token must contain only digits.");
    }
}

/// <summary>Invited supplier completing registration — validates invite token, PAN, password strength.</summary>
public class SupplierCompleteRegistrationValidator : AbstractValidator<SupplierCompleteRegistrationRequest>
{
    public SupplierCompleteRegistrationValidator()
    {
        RuleFor(x => x.InviteToken)
            .NotEmpty().WithMessage("Invite token is required.");

        RuleFor(x => x.ContactFullName)
            .NotEmpty().WithMessage("Full name is required.")
            .Length(2, 150).WithMessage("Full name must be between 2 and 150 characters.");

        RuleFor(x => x.JobTitle)
            .NotEmpty().WithMessage("Job title is required.")
            .MaximumLength(100).WithMessage("Job title cannot exceed 100 characters.");

        RuleFor(x => x.PAN)
            .NotEmpty().WithMessage("PAN is required.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$")
            .WithMessage("PAN must be a valid 10-character Indian format (e.g. ABCDE1234F).");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

/// <summary>Admin supplier review — validates action value and conditional approval fields.</summary>
public class SupplierReviewValidator : AbstractValidator<SupplierReviewRequest>
{
    private static readonly string[] AllowedActions = ["Approve", "Reject", "RequestMoreInfo"];

    public SupplierReviewValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .Must(a => AllowedActions.Contains(a, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Action must be one of: Approve, Reject, RequestMoreInfo.");

        // On Approve: Code is required and must match format
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Supplier Code is required for approval.")
            .Matches(@"^[A-Z0-9-]{3,20}$")
            .WithMessage("Supplier Code must be 3–20 characters, uppercase letters, digits, or hyphens.")
            .When(x => x.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase));

        // On Reject / RequestMoreInfo: Reason is required
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for rejection.")
            .When(x => x.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A message is required when requesting more information.")
            .When(x => x.Action.Equals("RequestMoreInfo", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Forgot password — validates email format.</summary>
public class SupplierForgotPasswordValidator : AbstractValidator<SupplierForgotPasswordRequest>
{
    public SupplierForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

/// <summary>Reset password — validates token presence and new password strength.</summary>
public class SupplierResetPasswordValidator : AbstractValidator<SupplierResetPasswordRequest>
{
    public SupplierResetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

/// <summary>Refresh token — ensures the token string is present.</summary>
public class SupplierRefreshTokenValidator : AbstractValidator<SupplierRefreshTokenRequest>
{
    public SupplierRefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

/// <summary>Resend OTP — ensures a valid, non-empty ContactId is provided.</summary>
public class SupplierResendOtpValidator : AbstractValidator<SupplierResendOtpRequest>
{
    public SupplierResendOtpValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("Contact ID is required.");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// AUTH VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

public class SupplierLoginValidator : AbstractValidator<SupplierLoginRequest>
{
    public SupplierLoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class SupplierChangePasswordValidator : AbstractValidator<SupplierChangePasswordRequest>
{
    public SupplierChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// PURCHASE ORDER VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

public class SupplierRespondToPOValidator : AbstractValidator<SupplierRespondToPORequest>
{
    public SupplierRespondToPOValidator()
    {
        RuleFor(x => x.DeclineReason)
            .NotEmpty().WithMessage("A decline reason is required when rejecting a PO.")
            .When(x => !x.Accept);

        RuleFor(x => x.CommittedDeliveryDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Committed delivery date must be in the future.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddYears(1)).WithMessage("Committed delivery date cannot be more than 1 year in the future.")
            .When(x => x.Accept && x.CommittedDeliveryDate.HasValue);
    }
}

public class SupplierUpdateDeliveryDateValidator : AbstractValidator<SupplierUpdateDeliveryDateRequest>
{
    public SupplierUpdateDeliveryDateValidator()
    {
        RuleFor(x => x.ExpectedDelivery)
            .GreaterThan(DateTime.UtcNow).WithMessage("Expected delivery date must be in the future.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddYears(1)).WithMessage("Expected delivery date cannot be more than 1 year in the future.");

        RuleFor(x => x.SupplierNotes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.SupplierNotes != null);
    }
}

public class SupplierMarkDispatchedValidator : AbstractValidator<SupplierMarkDispatchedRequest>
{
    public SupplierMarkDispatchedValidator()
    {
        RuleFor(x => x.TrackingNumber)
            .MaximumLength(100).WithMessage("Tracking number cannot exceed 100 characters.")
            .When(x => x.TrackingNumber != null);

        RuleFor(x => x.SupplierNotes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.SupplierNotes != null);
    }
}

public class SupplierCreateShipmentValidator : AbstractValidator<SupplierCreateShipmentRequest>
{
    public SupplierCreateShipmentValidator()
    {
        RuleFor(x => x.TrackingNumber)
            .MaximumLength(100).When(x => x.TrackingNumber != null);

        RuleFor(x => x.CarrierName)
            .MaximumLength(100).When(x => x.CarrierName != null);

        RuleFor(x => x.SupplierNotes)
            .MaximumLength(500).When(x => x.SupplierNotes != null);

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PurchaseOrderItemId).NotEmpty();
            line.RuleFor(l => l.QuantityDispatched).GreaterThan(0);
        }).When(x => x.Lines != null);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// INVOICE VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

public class SupplierUploadInvoiceValidator : AbstractValidator<SupplierUploadInvoiceRequest>
{
    private static readonly string[] AllowedMimeTypes = ["application/pdf"];

    public SupplierUploadInvoiceValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty().WithMessage("Purchase Order ID is required.");

        RuleFor(x => x.InvoiceNumber)
            .NotEmpty().WithMessage("Invoice number is required.")
            .MaximumLength(50).WithMessage("Invoice number cannot exceed 50 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Invoice amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be a 3-character ISO code (e.g. USD).");

        RuleFor(x => x.InvoiceDate)
            .NotEmpty().WithMessage("Invoice date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Invoice date cannot be in the future.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Invoice PDF file is required.");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedMimeTypes.Contains(ct)).WithMessage("Only PDF files are accepted.")
            .When(x => !string.IsNullOrEmpty(x.ContentType));
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// CATALOGUE VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

public class SupplierUpdateCatalogueItemValidator : AbstractValidator<SupplierUpdateCatalogueItemRequest>
{
    public SupplierUpdateCatalogueItemValidator()
    {
        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price must be greater than zero.");

        RuleFor(x => x.LeadTimeDays)
            .GreaterThan(0).WithMessage("Lead time must be at least 1 day.")
            .LessThanOrEqualTo(365).WithMessage("Lead time cannot exceed 365 days.");

        RuleFor(x => x.MinOrderQuantity)
            .GreaterThan(0).WithMessage("Minimum order quantity must be at least 1.");
    }
}

public class SupplierAddCatalogueItemValidator : AbstractValidator<SupplierAddCatalogueItemRequest>
{
    public SupplierAddCatalogueItemValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price must be greater than zero.");

        RuleFor(x => x.LeadTimeDays)
            .GreaterThan(0).WithMessage("Lead time must be at least 1 day.")
            .LessThanOrEqualTo(365).WithMessage("Lead time cannot exceed 365 days.");

        RuleFor(x => x.MinOrderQuantity)
            .GreaterThan(0).WithMessage("Minimum order quantity must be at least 1.");
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// PROFILE VALIDATORS
// ──────────────────────────────────────────────────────────────────────────────

public class SupplierUpdateProfileValidator : AbstractValidator<SupplierUpdateProfileRequest>
{
    public SupplierUpdateProfileValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Phone)
            .Matches(@"^\+91[6-9]\d{9}$").WithMessage("Phone number must be a valid Indian phone number (e.g. +919876543210).")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.JobTitle)
            .MaximumLength(100).WithMessage("Job title cannot exceed 100 characters.")
            .When(x => x.JobTitle != null);
    }
}
