namespace SmartInventory.Core.DTOs.SupplierPortal;


// REQUEST DTOs


public record SupplierLoginRequest(
    string Email,
    string Password
);

public record SupplierRefreshTokenRequest(
    string RefreshToken
);

public record SupplierResendOtpRequest(
    Guid ContactId
);

public record SupplierChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);




// RESPONSE DTOs

public record SupplierAuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    SupplierContactInfo Contact
);

public record SupplierContactInfo(
    Guid ContactId,
    Guid SupplierId,
    string FullName,
    string Email,
    string SupplierName,
    string SupplierCode
);
