using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
/// Authentication contract for the supplier self-service portal.
/// Handles separate JWT issuance for SupplierContact principals.
public interface ISupplierAuthService
{

    Task<SupplierAuthResponse> LoginAsync(SupplierLoginRequest request, string ipAddress);


    Task<SupplierAuthResponse> RefreshTokenAsync(SupplierRefreshTokenRequest request, string ipAddress);


    Task RevokeTokenAsync(string token, string ipAddress);


    Task ChangePasswordAsync(Guid contactId, SupplierChangePasswordRequest request);


    Task<Guid> RegisterAsync(SupplierRegisterRequest request);


    Task VerifyEmailAsync(SupplierVerifyEmailRequest request);


    Task ResendOtpAsync(Guid contactId);


    Task CompleteRegistrationAsync(SupplierCompleteRegistrationRequest request);



    Task ForgotPasswordAsync(string email);


    Task ResetPasswordAsync(string token, string newPassword);
}
