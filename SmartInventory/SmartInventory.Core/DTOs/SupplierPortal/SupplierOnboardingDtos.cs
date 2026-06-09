using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

//Request DTO for supplier self-registration.
public record SupplierRegisterRequest(
    string Name,
    string GSTIN,
    string PAN,
    string Address,

    string ContactFullName,
    string Email,
    string Phone,
    string Password
);

public record SupplierVerifyEmailRequest(
    string Email,
    string Token
);

public record SupplierInviteRequest(
    string Name,
    string GSTIN,
    string Email,
    string Phone
);

public record SupplierCompleteRegistrationRequest(
    string InviteToken,
    string ContactFullName,
    string JobTitle,
    string PAN,
    string Address,
    string Password
);

public record SupplierReviewRequest(
    string Action, 
    string? Reason,
    decimal? CreditLimit, 
    PaymentTerms? PaymentTerms
);

public record SupplierSubmitInfoRequest(
    string Message
);

public record SupplierSuspendRequest(
    string Reason
);

public record SupplierOnboardingStatusResponse(
    SupplierStatus Status,
    string StatusName,
    string? RejectionReason,
    string? SuspensionReason,
    string? InfoRequestedMessage,
    bool EmailVerified
);

public record SupplierForgotPasswordRequest(
    string Email
);

public record SupplierResetPasswordRequest(
    string Token,
    string NewPassword
);
