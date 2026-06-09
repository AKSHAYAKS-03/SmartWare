using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class UserServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ITokenBlacklistService> _blacklistMock;
    private readonly Mock<IEncryptionService> _encryptionMock;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _uow = new UnitOfWork(
            _context,
            Mock.Of<IProductRepository>(),
            Mock.Of<ISupplierRepository>(),
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ITransferRepository>(),
            Mock.Of<IBarcodeRepository>(),
            Mock.Of<INotificationRepository>(),
            Mock.Of<IStockLevelRepository>());

        _notificationMock = new Mock<INotificationService>();
        _notificationMock.Setup(n => n.SendWelcomeInviteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _blacklistMock = new Mock<ITokenBlacklistService>();
        _blacklistMock.Setup(b => b.BlacklistUserAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _encryptionMock = new Mock<IEncryptionService>();
        _encryptionMock.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(value => $"enc:{value}");

        _service = new UserService(_uow, _notificationMock.Object, _blacklistMock.Object, _encryptionMock.Object);
    }

    // Prevents admin onboarding from skipping invite generation and pending-verification state
    [Fact]
    public async Task CreateUserAsync_ValidUser_CreatesPendingUserAndSendsInvite()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var dto = new UserCreateDto
        {
            FullName = "Alice User",
            Email = "alice@test.com",
            PhoneNumber = "+919876543210",
            EmployeeId = "EMP-001",
            RoleId = role.Id,
            SmsEnabled = true,
            EmailEnabled = true,
            IsActive = true
        };

        // Act
        var result = await _service.CreateUserAsync(dto);

        // Assert
        Assert.Equal("alice@test.com", result.Email);
        Assert.Equal(UserStatus.PendingVerification, result.Status);
        Assert.False(result.IsPasswordSet);

        var saved = await _context.Users.FirstAsync(u => u.Email == "alice@test.com");
        Assert.False(string.IsNullOrWhiteSpace(saved.InviteToken));
        Assert.False(saved.IsPasswordSet);
        Assert.Equal(UserStatus.PendingVerification, saved.Status);
        _notificationMock.Verify(n => n.SendWelcomeInviteAsync(saved.Id, "alice@test.com", "Alice User", It.IsAny<string>()), Times.Once);
    }

    // Prevents duplicate employee accounts from being created with the same email address
    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsBusinessRuleException()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        await _context.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            FullName = "Existing",
            Email = "duplicate@test.com",
            PasswordHash = "hash",
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateUserAsync(new UserCreateDto
        {
            FullName = "New User",
            Email = "duplicate@test.com",
            PhoneNumber = "+919876543210",
            EmployeeId = "EMP-002",
            RoleId = role.Id,
            SmsEnabled = false,
            EmailEnabled = true,
            IsActive = true
        }));
    }

    // Prevents terminated users from remaining active in the auth surface
    [Fact]
    public async Task DeactivateUserAsync_ValidUser_BlacklistsAndSoftDeletes()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Inactive Me",
            Email = "inactive@test.com",
            PasswordHash = "hash",
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeactivateUserAsync(user.Id);

        // Assert
        var saved = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.False(saved.IsActive);
        Assert.Equal(UserStatus.Terminated, saved.Status);
        _blacklistMock.Verify(b => b.BlacklistUserAsync(user.Id), Times.Once);
    }

    // Prevents approvals from bypassing pending-verification workflow
    [Fact]
    public async Task ApproveUserAsync_PendingUser_ActivatesAndRecordsApprover()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == "Admin");
        var approver = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            RoleId = adminRole.Id,
            Role = adminRole,
            Status = UserStatus.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Pending User",
            Email = "pending@test.com",
            PasswordHash = string.Empty,
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.PendingVerification,
            IsActive = true,
            IsPasswordSet = false,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddRangeAsync(approver, user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ApproveUserAsync(user.Id, approver.Id);

        // Assert
        Assert.Equal(UserStatus.Active, result.Status);
        Assert.Equal(approver.Id, result.ApprovedById);
        var saved = await _context.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(UserStatus.Active, saved.Status);
        Assert.Equal(approver.Id, saved.ApprovedById);
    }

    // Prevents invite links from being resent after the employee already activated their account
    [Fact]
    public async Task ResendInviteAsync_PasswordAlreadySet_ThrowsBusinessRuleException()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Staff");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Activated User",
            Email = "activated@test.com",
            PasswordHash = "hash",
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            IsPasswordSet = true,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ResendInviteAsync(user.Id));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
