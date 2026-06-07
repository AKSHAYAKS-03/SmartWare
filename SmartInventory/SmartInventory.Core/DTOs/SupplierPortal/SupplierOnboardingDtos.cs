using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

/// <summary>Request DTO for supplier self-registration.</summary>
public record SupplierRegisterRequest(
    // Company Details
    string Name,
    string GSTIN,
    string PAN,
    string Address,

    // Contact & Login Details
    string ContactFullName,
    string Email,
    string Phone,
    string Password
);

/// <summary>Request DTO to verify a self-registered supplier email using an OTP token.</summary>
public record SupplierVerifyEmailRequest(
    string Email,
    string Token
);

/// <summary>Request DTO for admin to invite a new supplier.</summary>
public record SupplierInviteRequest(
    string Name,
    string GSTIN,
    string Email,
    string Phone
);

/// <summary>Request DTO for a supplier to complete registration via invite token.</summary>
public record SupplierCompleteRegistrationRequest(
    string InviteToken,
    string ContactFullName,
    string JobTitle,
    string PAN,
    string Address,
    string Password
);

/// <summary>Request DTO for admin to review a pending supplier registration.</summary>
public record SupplierReviewRequest(
    string Action, // "Approve", "Reject", "RequestMoreInfo"
    string? Reason, // Rejection reason, info request message, or review comments
    string? Code, // Must be supplied on Approval
    decimal? CreditLimit, // Must be supplied on Approval
    PaymentTerms? PaymentTerms // Must be supplied on Approval
);

/// <summary>Request DTO for supplier to submit requested info.</summary>
public record SupplierSubmitInfoRequest(
    string Message
);

/// <summary>Request DTO for suspending a supplier account.</summary>
public record SupplierSuspendRequest(
    string Reason
);

/// <summary>Response DTO representing onboarding status.</summary>
public record SupplierOnboardingStatusResponse(
    SupplierStatus Status,
    string StatusName,
    string? RejectionReason,
    string? SuspensionReason,
    string? InfoRequestedMessage,
    bool EmailVerified
);

/// <summary>Request DTO for forgot password.</summary>
public record SupplierForgotPasswordRequest(
    string Email
);

/// <summary>Request DTO for reset password.</summary>
public record SupplierResetPasswordRequest(
    string Token,
    string NewPassword
);
