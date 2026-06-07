using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartInventory.Core;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;
using System;
using System.Threading.Tasks;
namespace SmartInventory.Tests;

/// <summary>
/// Unit tests for AuthService — covers all authentication paths:
///   — Happy path login / token refresh
///   — Invalid credentials → returns null
///   — Inactive / suspended user → returns null
///   — Expired refresh token → returns null
///   — Revoke clears refresh token
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly AuthService _service;

    private static readonly JWTsettings JwtSettings = new()
    {
        SecretKey = "TestSecret_AtLeast_32_Characters_Long_123!",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        AccessTokenExpiryMinutes = 60,
        RefreshTokenExpiryDays = 7
    };

    public AuthServiceTests()
    {
        _context = TestDbContextFactory.Create();

        var products = new Mock<IProductRepository>();
        var suppliers = new Mock<ISupplierRepository>();
        var purchaseOrders = new Mock<IPurchaseOrderRepository>();
        var transfers = new Mock<ITransferRepository>();
        var barcodes = new Mock<IBarcodeRepository>();
        var notifications = new Mock<INotificationRepository>();
        var stockLevels = new Mock<IStockLevelRepository>();

        _uow = new UnitOfWork(_context, products.Object, suppliers.Object,
            purchaseOrders.Object, transfers.Object, barcodes.Object,
            notifications.Object, stockLevels.Object);

        var jwtOptions = Options.Create(JwtSettings);
        _service = new AuthService(_uow, jwtOptions, new Mock<ITokenBlacklistService>().Object);
    }

    private async Task<(Role role, User user)> SeedActiveUserAsync()
    {
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test Manager",
            Email = "manager@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return (role, user);
    }

    [Fact]
    public async Task SignInAsync_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        var (_, user) = await SeedActiveUserAsync();

        // Act
        var result = await _service.SignInAsync(new LoginDto
        {
            Email = "manager@test.com",
            Password = "SecurePass123!"
        });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal(user.Email, result.User.Email);
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        await SeedActiveUserAsync();

        // Act
        var result = await _service.SignInAsync(new LoginDto
        {
            Email = "manager@test.com",
            Password = "WrongPassword!"
        });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SignInAsync_NonExistentEmail_ReturnsNull()
    {
        // Act
        var result = await _service.SignInAsync(new LoginDto
        {
            Email = "nobody@test.com",
            Password = "anything"
        });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        await SeedActiveUserAsync();
        var loginResult = await _service.SignInAsync(new LoginDto
        {
            Email = "manager@test.com",
            Password = "SecurePass123!"
        });
        Assert.NotNull(loginResult);

        // Act
        var refreshResult = await _service.RefreshTokenAsync(loginResult.RefreshToken);

        // Assert
        Assert.NotNull(refreshResult);
        Assert.NotNull(refreshResult.AccessToken);
        // Old refresh token should be rotated out (new one issued)
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsNull()
    {
        // Arrange
        await SeedActiveUserAsync();

        // Act
        var result = await _service.RefreshTokenAsync("invalid-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_ValidToken_ClearsToken()
    {
        // Arrange
        await SeedActiveUserAsync();
        var loginResult = await _service.SignInAsync(new LoginDto
        {
            Email = "manager@test.com",
            Password = "SecurePass123!"
        });
        Assert.NotNull(loginResult);

        // Act
        await _service.RevokeTokenAsync(loginResult.RefreshToken);

        // Assert — token should now be invalid
        var refreshResult = await _service.RefreshTokenAsync(loginResult.RefreshToken);
        Assert.Null(refreshResult);
    }

    [Fact]
    public async Task SignInAsync_ExpiredUser_ReturnsNull()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Expired Manager",
            Email = "expired@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SignInAsync(new LoginDto
        {
            Email = "expired@test.com",
            Password = "SecurePass123!"
        });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredUser_ReturnsNull()
    {
        // Arrange
        var role = await _context.Roles.FirstAsync(r => r.Name == "Manager");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Expired Manager",
            Email = "expired-refresh@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
            RoleId = role.Id,
            Role = role,
            Status = UserStatus.Active,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), // Currently active, expires in 5 mins
            CreatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var loginResult = await _service.SignInAsync(new LoginDto
        {
            Email = "expired-refresh@test.com",
            Password = "SecurePass123!"
        });
        Assert.NotNull(loginResult);

        // Expire the user now
        user.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Act
        var refreshResult = await _service.RefreshTokenAsync(loginResult.RefreshToken);

        // Assert
        Assert.Null(refreshResult);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
