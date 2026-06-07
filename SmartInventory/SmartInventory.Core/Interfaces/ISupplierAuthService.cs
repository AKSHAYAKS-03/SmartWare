using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Authentication contract for the supplier self-service portal.
/// Handles separate JWT issuance for SupplierContact principals.
/// </summary>
public interface ISupplierAuthService
{
    /// <summary>Validates credentials and issues a supplier-scoped access + refresh token pair.</summary>
    Task<SupplierAuthResponse> LoginAsync(SupplierLoginRequest request, string ipAddress);

    /// <summary>Issues a new access token using a valid, unexpired supplier refresh token.</summary>
    Task<SupplierAuthResponse> RefreshTokenAsync(SupplierRefreshTokenRequest request, string ipAddress);

    /// <summary>Revokes a supplier refresh token (logout).</summary>
    Task RevokeTokenAsync(string token, string ipAddress);

    /// <summary>Changes the password for the currently authenticated supplier contact.</summary>
    Task ChangePasswordAsync(Guid contactId, SupplierChangePasswordRequest request);

    /// <summary>Handles self-registration of a new supplier and their initial contact. Returns the new ContactId.</summary>
    Task<Guid> RegisterAsync(SupplierRegisterRequest request);

    /// <summary>Verifies a supplier contact's email address via OTP.</summary>
    Task VerifyEmailAsync(SupplierVerifyEmailRequest request);

    /// <summary>Resends OTP to a supplier contact (max 3 times).</summary>
    Task ResendOtpAsync(Guid contactId);

    /// <summary>Completes registration and sets password for an admin-invited supplier.</summary>
    Task CompleteRegistrationAsync(SupplierCompleteRegistrationRequest request);

    // ─── Password Reset Flow ──────────────────────────────────────────────────

    /// <summary>Initiates password reset for a supplier contact.</summary>
    Task ForgotPasswordAsync(string email);

    /// <summary>Resets password using a valid reset token.</summary>
    Task ResetPasswordAsync(string token, string newPassword);
}
