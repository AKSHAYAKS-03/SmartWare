using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SmartInventory.Core;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Service.Services;
using Xunit;

namespace SmartInventory.Tests;

public class SupplierAuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly SupplierAuthService _service;

    private static readonly JWTsettings JwtSettings = new()
    {
        SecretKey = "TestSecret_AtLeast_32_Characters_Long_123!",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        AccessTokenExpiryMinutes = 60,
        RefreshTokenExpiryDays = 7
    };

    public SupplierAuthServiceTests()
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
        _notificationMock.Setup(n => n.SendPasswordResetSuccessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        _service = new SupplierAuthService(_uow, Options.Create(JwtSettings), _notificationMock.Object, _emailMock.Object);
    }

    // Prevents self-registered suppliers from bypassing email verification on first login
    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesSupplierAndContact()
    {
        // Arrange
        var request = new SupplierRegisterRequest(
            Name: "Acme Supplies",
            GSTIN: "27ABCDE1234F1Z5",
            PAN: "ABCDE1234F",
            Address: "123 Supplier Lane",
            ContactFullName: "Priya Contact",
            Email: "supplier-register@test.com",
            Phone: "+919876543210",
            Password: "SecurePass123!");

        // Act
        var contactId = await _service.RegisterAsync(request);

        // Assert
        var contact = await _context.SupplierContacts.Include(c => c.Supplier).FirstAsync(c => c.Id == contactId);
        Assert.Equal(request.Email, contact.Email);
        Assert.False(contact.EmailVerified);
        Assert.Equal(SupplierStatus.Registered, contact.Supplier.Status);
        Assert.False(string.IsNullOrWhiteSpace(contact.Supplier.Code));
        _emailMock.Verify(e => e.SendEmailAsync(
            request.Email,
            "SmartInventory - Email Verification",
            It.IsAny<string>(),
            true), Times.Once);
    }

    // Prevents supplier portal sign-in from accepting invalid credentials
    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSupplierTokens()
    {
        // Arrange
        var (_, contact) = await SeedActiveSupplierContactAsync();

        // Act
        var result = await _service.LoginAsync(new SupplierLoginRequest(contact.Email, "SecurePass123!"), "127.0.0.1");

        // Assert
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal(contact.Id, result.Contact.ContactId);
        Assert.Equal(contact.SupplierId, result.Contact.SupplierId);
        Assert.Equal(contact.Email, result.Contact.Email);
    }

    private async Task<(Supplier Supplier, SupplierContact Contact)> SeedActiveSupplierContactAsync()
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Active Supplier",
            Code = "SUP-ACTIVE-1",
            GSTIN = "27ABCDE1234F1Z5",
            PAN = "ABCDE1234F",
            Email = "supplier-login@test.com",
            Phone = "+919876543210",
            Address = "123 Supplier Lane",
            IsActive = true,
            Status = SupplierStatus.Active,
            RegistrationSource = RegistrationSource.AdminInvited,
            CreatedAt = DateTime.UtcNow
        };

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Supplier = supplier,
            FullName = "Active Contact",
            Email = supplier.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
            Phone = supplier.Phone,
            JobTitle = "Primary Contact",
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        supplier.Contacts.Add(contact);

        await _context.Suppliers.AddAsync(supplier);
        await _context.SupplierContacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        return (supplier, contact);
    }

    // Prevents a self-registered supplier from escaping OTP lockout after repeated bad codes
    [Fact]
    public async Task VerifyEmailAsync_WrongToken_LocksAccountAfterMaxRetries()
    {
        // Arrange
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Registered Supplier",
            Code = "SUP-LOCK-1",
            GSTIN = "27ABCDE1234F1Z5",
            PAN = "ABCDE1234F",
            Email = "locked@test.com",
            Phone = "+919876543210",
            Address = "123 Supplier Lane",
            IsActive = true,
            Status = SupplierStatus.Registered,
            RegistrationSource = RegistrationSource.SelfRegistered,
            CreatedAt = DateTime.UtcNow
        };

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Supplier = supplier,
            FullName = "Locked Contact",
            Email = supplier.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
            Phone = supplier.Phone,
            IsActive = true,
            EmailVerified = false,
            EmailVerifyToken = "111111",
            EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(10),
            OtpRetryCount = 2,
            OtpMaxRetries = 3,
            CreatedAt = DateTime.UtcNow
        };

        supplier.Contacts.Add(contact);
        await _context.Suppliers.AddAsync(supplier);
        await _context.SupplierContacts.AddAsync(contact);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.VerifyEmailAsync(new SupplierVerifyEmailRequest(
            Email: supplier.Email,
            Token: "000000")));

        var saved = await _context.SupplierContacts.FirstAsync(c => c.Id == contact.Id);
        Assert.True(saved.OtpLockedUntil.HasValue);
        Assert.Equal(3, saved.OtpRetryCount);
        Assert.NotEqual("111111", saved.EmailVerifyToken);
    }

    // Prevents reset links from being reused after a successful supplier password reset
    [Fact]
    public async Task ResetPasswordAsync_ValidToken_RevokesRefreshTokensAndClearsResetToken()
    {
        // Arrange
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "Reset Supplier",
            Code = "SUP-RESET-1",
            Email = "reset@test.com",
            Phone = "+919876543210",
            Address = "123 Supplier Lane",
            IsActive = true,
            Status = SupplierStatus.Active,
            RegistrationSource = RegistrationSource.AdminInvited,
            CreatedAt = DateTime.UtcNow
        };

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Supplier = supplier,
            FullName = "Reset Contact",
            Email = supplier.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!"),
            Phone = supplier.Phone,
            IsActive = true,
            EmailVerified = true,
            EmailVerifyToken = "reset-token-123",
            EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };

        var token = new SupplierRefreshToken
        {
            Id = Guid.NewGuid(),
            SupplierContactId = contact.Id,
            SupplierContact = contact,
            Token = "refresh-token-123",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        supplier.Contacts.Add(contact);
        contact.RefreshTokens.Add(token);

        await _context.Suppliers.AddAsync(supplier);
        await _context.SupplierContacts.AddAsync(contact);
        await _context.SupplierRefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();

        // Act
        await _service.ResetPasswordAsync("reset-token-123", "NewPass123!");

        // Assert
        var savedContact = await _context.SupplierContacts.FirstAsync(c => c.Id == contact.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", savedContact.PasswordHash));
        Assert.Null(savedContact.EmailVerifyToken);
        Assert.Null(savedContact.EmailVerifyExpiresAt);
        var savedToken = await _context.SupplierRefreshTokens.FirstAsync(t => t.Id == token.Id);
        Assert.True(savedToken.IsRevoked);
        Assert.Equal("Password reset", savedToken.RevokedReason);
        _notificationMock.Verify(n => n.SendPasswordResetSuccessAsync(contact.Id, contact.Email, contact.FullName), Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
