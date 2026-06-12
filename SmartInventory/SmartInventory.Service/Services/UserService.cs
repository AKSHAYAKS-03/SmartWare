using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;
using System.Security.Cryptography;
using System.Text;

namespace SmartInventory.Service.Services;


public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly IEncryptionService _encryptionService;

    public UserService(IUnitOfWork uow, INotificationService notificationService, ITokenBlacklistService blacklistService, IEncryptionService encryptionService)
    {
        _uow = uow;
        _notificationService = notificationService;
        _blacklistService = blacklistService;
        _encryptionService = encryptionService;
    }

    public async Task<UserResponseDto> CreateUserAsync(UserCreateDto dto)
    {
        bool emailExists = await _uow.Repository<User>()
            .Query().AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            throw new BusinessRuleException($"Email '{dto.Email}' is already in use.");

        var role = await _uow.Repository<Role>().GetByIdAsync(dto.RoleId);
        if (role == null) throw new NotFoundException("Role", dto.RoleId);

        // Generate a cryptographically secure one-time invite token (64 hex chars)
        var plainInviteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hashedInviteToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainInviteToken)));

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = string.Empty,   
            PhoneNumber = dto.PhoneNumber,
            EmployeeId = dto.EmployeeId,
            RoleId = dto.RoleId,
            SmsEnabled = dto.SmsEnabled,
            EmailEnabled = dto.EmailEnabled,
            IsActive = dto.IsActive,
            IsPasswordSet = false,
            InviteToken = hashedInviteToken,
            InviteTokenExpiresAt = DateTime.UtcNow.AddHours(48),
            Status = UserStatus.PendingVerification,
            ExpiresAt = dto.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(dto.AadhaarNumber))
        {
            user.AadhaarLastFour = dto.AadhaarNumber.Substring(8);
            user.EncryptedAadhaarNumber = _encryptionService.Encrypt(dto.AadhaarNumber);
        }

        await _uow.Repository<User>().AddAsync(user);
        await _uow.CommitAsync();

        user.Role = role;

        await _notificationService.SendWelcomeInviteAsync(user.Id, user.Email, user.FullName, plainInviteToken);

        return user.Adapt<UserResponseDto>();
    }

    public async Task<UserResponseDto> UpdateUserAsync(Guid userId, UserUpdateDto dto)
    {
        var user = await _uow.Repository<User>()
            .Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new NotFoundException("User", userId);

        // Check email uniqueness if changing it
        bool emailConflict = await _uow.Repository<User>()
            .Query().AnyAsync(u => u.Email == dto.Email && u.Id != userId);
        if (emailConflict)
            throw new BusinessRuleException($"Email '{dto.Email}' is already used by another user.");

        var role = await _uow.Repository<Role>().GetByIdAsync(dto.RoleId);
        if (role == null) throw new NotFoundException("Role", dto.RoleId);

        user.FullName = dto.FullName;
        user.Email = dto.Email;
        user.PhoneNumber = dto.PhoneNumber;
        user.EmployeeId = dto.EmployeeId;
        user.RoleId = dto.RoleId;
        user.SmsEnabled = dto.SmsEnabled;
        user.EmailEnabled = dto.EmailEnabled;
        user.IsActive = dto.IsActive;
        user.ExpiresAt = dto.ExpiresAt;

        if (!string.IsNullOrEmpty(dto.AadhaarNumber))
        {
            user.AadhaarLastFour = dto.AadhaarNumber.Substring(8);
            user.EncryptedAadhaarNumber = _encryptionService.Encrypt(dto.AadhaarNumber);
        }
        user.Role = role;

        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();
        return user.Adapt<UserResponseDto>();
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        user.IsActive = false;
        user.Status = UserStatus.Terminated; // Terminated = effectively deactivated in the enum
        _uow.Repository<User>().Update(user);
        await _blacklistService.BlacklistUserAsync(userId);
        await _uow.CommitAsync();
    }

    public async Task<UserResponseDto> GetUserByIdAsync(Guid userId)
    {
        var user = await _uow.Repository<User>()
            .Query()
            .Include(u => u.Role)
            .Include(u => u.ApprovedBy)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) throw new NotFoundException("User", userId);
        return user.Adapt<UserResponseDto>();
    }

    public async Task<PagedResult<UserResponseDto>> GetUsersAsync(QueryParameters queryParams)
    {
        IQueryable<User> query = _uow.Repository<User>().Query().Include(u => u.Role);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(u => u.FullName.Contains(queryParams.Search) ||
                                     u.Email.Contains(queryParams.Search) ||
                                     (u.EmployeeId != null && u.EmployeeId.Contains(queryParams.Search)));

        int total = await query.CountAsync();
        var data = await query
            .OrderBy(u => u.FullName)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<UserResponseDto>
        {
            Data = data.Adapt<IEnumerable<UserResponseDto>>(),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }


    public async Task<UserResponseDto> ApproveUserAsync(Guid userId, Guid approvedBy)
    {
        var user = await _uow.Repository<User>()
            .Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new NotFoundException("User", userId);

        if (user.Status != UserStatus.PendingVerification)
            throw new BusinessRuleException("Only users in PendingVerification status can be approved.");

        var approver = await _uow.Repository<User>().GetByIdAsync(approvedBy);
        if (approver == null) throw new NotFoundException("Approver User", approvedBy);

        user.Status = UserStatus.Active;
        user.ApprovedById = approvedBy;
        user.ApprovedAt = DateTime.UtcNow;

        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();
        return user.Adapt<UserResponseDto>();
    }

    public async Task UpdateNotificationPreferencesAsync(
        Guid userId, bool smsEnabled, bool emailEnabled)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        user.SmsEnabled = smsEnabled;
        user.EmailEnabled = emailEnabled;
        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();
    }

    public async Task ResendInviteAsync(Guid userId)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        if (user.IsPasswordSet)
            throw new BusinessRuleException("This user has already set their password and activated their account.");

        // Generate a fresh token and reset the 48-hour window
        var plainInviteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.InviteToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainInviteToken)));
        user.InviteTokenExpiresAt = DateTime.UtcNow.AddHours(48);
        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();

        await _notificationService.SendWelcomeInviteAsync(user.Id, user.Email, user.FullName, plainInviteToken);
    }

}
