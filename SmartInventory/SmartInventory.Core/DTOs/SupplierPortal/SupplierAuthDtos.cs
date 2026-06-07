namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// REQUEST DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier portal login request.</summary>
public record SupplierLoginRequest(
    string Email,
    string Password
);

/// <summary>Token refresh request using an existing supplier refresh token.</summary>
public record SupplierRefreshTokenRequest(
    string RefreshToken
);

/// <summary>Resend email OTP request. ContactId is returned in the Register response.</summary>
public record SupplierResendOtpRequest(
    Guid ContactId
);

/// <summary>Password change request from within the portal.</summary>
public record SupplierChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Successful portal login response.</summary>
public record SupplierAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    SupplierContactInfo Contact
);

/// <summary>Identity information included in the auth token response.</summary>
public record SupplierContactInfo(
    Guid ContactId,
    Guid SupplierId,
    string FullName,
    string Email,
    string SupplierName,
    string SupplierCode
);
