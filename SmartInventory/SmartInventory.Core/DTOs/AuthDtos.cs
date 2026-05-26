using System;
using SmartInventory.Core.Enums;


namespace SmartInventory.Core.DTOs;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserResponseDto User { get; set; } = null!;
}

public class RegisterDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmployeeId { get; set; }
    public Guid RoleId { get; set; }
}

public class RefreshTokenDto
{
    public string Token { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UserCreateDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmployeeId { get; set; }
    public Guid RoleId { get; set; }
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserUpdateDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmployeeId { get; set; }
    public Guid RoleId { get; set; }
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool IsActive { get; set; }
}

public class UserResponseDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? EmployeeId { get; set; }
    public UserStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool IsActive { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
public class AuditLogQueryParameters : QueryParameters
{
    public Guid? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class AuditLogResponseDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
