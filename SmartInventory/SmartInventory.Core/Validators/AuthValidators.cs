using FluentValidation;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Validators;

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(255).WithMessage("Password must not exceed 255 characters.");
    }
}



public class UserCreateValidator : AbstractValidator<UserCreateDto>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .Length(2, 100).WithMessage("Full Name must be between 2 and 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        // No Password rule — employee sets their own via the invite email link.

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role is required.");

        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required for corporate vetting.")
            .Matches(@"^EMP-\d{5,10}$").WithMessage("Employee ID must follow corporate format, e.g. EMP-12345.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+91[6-9]\d{9}$").WithMessage("Phone number must be a valid Indian phone number (e.g. +919876543210).")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.AadhaarNumber)
            .Matches(@"^\d{12}$").WithMessage("Aadhaar must be exactly 12 digits.")
            .When(x => !string.IsNullOrEmpty(x.AadhaarNumber));
    }
}

public class UserUpdateValidator : AbstractValidator<UserUpdateDto>
{
    public UserUpdateValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .Length(2, 100).WithMessage("Full Name must be between 2 and 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role is required.");

        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required for corporate vetting.")
            .Matches(@"^EMP-\d{5,10}$").WithMessage("Employee ID must follow corporate format, e.g. EMP-12345.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+91[6-9]\d{9}$").WithMessage("Phone number must be a valid Indian phone number (e.g. +919876543210).")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.AadhaarNumber)
            .Matches(@"^\d{12}$").WithMessage("Aadhaar must be exactly 12 digits.")
            .When(x => !string.IsNullOrEmpty(x.AadhaarNumber));
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Old password is required.")
            .MaximumLength(255).WithMessage("Old password must not exceed 255 characters.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(255).WithMessage("Password must not exceed 255 characters.")
            .NotEqual(x => x.OldPassword).WithMessage("New password cannot be the same as the old password.");
    }
}

/// <summary>
/// Validates the one-time invite token and the employee's chosen password.
/// Applied to POST /api/v1/Auth/set-password.
/// </summary>
public class SetPasswordValidator : AbstractValidator<SetPasswordDto>
{
    public SetPasswordValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required.")
            .MaximumLength(500).WithMessage("Token must not exceed 500 characters.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(255).WithMessage("Password must not exceed 255 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}
