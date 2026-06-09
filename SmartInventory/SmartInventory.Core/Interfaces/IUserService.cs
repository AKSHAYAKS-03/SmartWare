using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> CreateUserAsync(UserCreateDto dto);
    Task<UserResponseDto> UpdateUserAsync(Guid userId, UserUpdateDto dto);
    Task DeactivateUserAsync(Guid userId);
    Task<UserResponseDto> GetUserByIdAsync(Guid userId);
    Task<PagedResult<UserResponseDto>> GetUsersAsync(QueryParameters queryParams);
    Task<UserResponseDto> ApproveUserAsync(Guid userId, Guid approvedBy);
    Task UpdateNotificationPreferencesAsync(Guid userId, bool smsEnabled, bool emailEnabled);

    // Regenerates the invite token and resends the welcome email.
    // Only valid if the employee has not yet set their password (IsPasswordSet = false).
    Task ResendInviteAsync(Guid userId);
}
