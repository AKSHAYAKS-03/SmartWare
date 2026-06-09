using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IAuthService
{

    Task<LoginResponseDto?> SignInAsync(LoginDto dto);

    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);

    Task RevokeTokenAsync(string refreshToken);

    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    Task SetPasswordAsync(SetPasswordDto dto);
    }