using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartInventory.Core;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Core.Enums;

namespace SmartInventory.Service.Services;


public class SupplierAuthService : ISupplierAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly JWTsettings _jwtSettings;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public SupplierAuthService(IUnitOfWork uow, IOptions<JWTsettings> jwtOptions, INotificationService notificationService, IEmailService emailService)
    {
        _uow = uow;
        _jwtSettings = jwtOptions.Value;
        _notificationService = notificationService;
        _emailService = emailService;
    }



    public async Task<SupplierAuthResponse> LoginAsync(SupplierLoginRequest request, string ipAddress)
    {
        // 1. Find active SupplierContact by email
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Email == request.Email && c.IsActive);

        if (contact == null || contact.Supplier == null)
            throw new BusinessRuleException("Invalid credentials or account is not active.");

        if (contact.Supplier.Status != SupplierStatus.Active && contact.Supplier.Status != SupplierStatus.AgreementPending)
        {
            throw new BusinessRuleException(contact.Supplier.Status switch
            {
                SupplierStatus.Registered => "Please verify your email before logging in.",
                SupplierStatus.InviteSent => "Please complete your registration via the invite link.",
                SupplierStatus.PendingReview => "Your account is pending administrator review.",
                SupplierStatus.Suspended => $"Your account is suspended. Reason: {contact.Supplier.SuspensionReason}",
                SupplierStatus.Rejected => $"Your application was rejected. Reason: {contact.Supplier.RejectionReason}",
                SupplierStatus.InfoRequested => "More information is requested. Please check your email.",
                _ => "Your account is not active."
            });
        }

        // 2. Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, contact.PasswordHash))
            throw new BusinessRuleException("Invalid credentials or account is not active.");

        // 3. Issue tokens
        var accessToken = GenerateAccessToken(contact);
        var refreshToken = await CreateRefreshTokenAsync(contact.Id, ipAddress);

        // 4. Update last login
        contact.LastLoginAt = DateTime.UtcNow;
        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();

        return BuildAuthResponse(accessToken, refreshToken.Token, contact);
    }

    public async Task<SupplierAuthResponse> RefreshTokenAsync(SupplierRefreshTokenRequest request, string ipAddress)
    {
        // 1. Find valid, non-expired, non-revoked supplier refresh token
        var storedToken = await _uow.Repository<SupplierRefreshToken>().Query()
            .Include(t => t.SupplierContact)
                .ThenInclude(c => c.Supplier)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (storedToken == null)
            throw new NotFoundException("SupplierRefreshToken", request.RefreshToken);

        if (storedToken.IsRevoked)
            throw new BusinessRuleException("Refresh token has been revoked.");

        if (storedToken.ExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("Refresh token has expired. Please log in again.");

        var contact = storedToken.SupplierContact;

        // 2. Rotate: revoke old, issue new token pair
        storedToken.IsRevoked = true;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevokedReason = "Rotated on refresh";
        _uow.Repository<SupplierRefreshToken>().Update(storedToken);

        var newAccessToken = GenerateAccessToken(contact);
        var newRefreshToken = await CreateRefreshTokenAsync(contact.Id, ipAddress);

        await _uow.CommitAsync();

        return BuildAuthResponse(newAccessToken, newRefreshToken.Token, contact);
    }


    public async Task RevokeTokenAsync(string token, string ipAddress)
    {
        var storedToken = await _uow.Repository<SupplierRefreshToken>().Query()
            .FirstOrDefaultAsync(t => t.Token == token);

        if (storedToken == null || storedToken.IsRevoked) return;

        storedToken.IsRevoked = true;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevokedReason = "Explicit logout";
        _uow.Repository<SupplierRefreshToken>().Update(storedToken);
        await _uow.CommitAsync();
    }


    public async Task ChangePasswordAsync(Guid contactId, SupplierChangePasswordRequest request)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.IsActive);

        if (contact == null)
            throw new NotFoundException("SupplierContact", contactId);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, contact.PasswordHash))
            throw new BusinessRuleException("Current password is incorrect.");

        // Hash new password and save
        contact.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        _uow.Repository<SupplierContact>().Update(contact);

        // Revoke ALL existing refresh tokens — force re-login on all devices
        var activeTokens = await _uow.Repository<SupplierRefreshToken>().Query()
            .Where(t => t.SupplierContactId == contactId && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
            t.RevokedReason = "Password changed";
            _uow.Repository<SupplierRefreshToken>().Update(t);
        }

        await _uow.CommitAsync();
    }


    private string GenerateAccessToken(SupplierContact contact)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, contact.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, contact.Email),
            new(ClaimTypes.Role, "Supplier"),
            new("role", "Supplier"),
            new("Permission", "Supplier"),
            new("contactId", contact.Id.ToString()),
            new("supplierId", contact.SupplierId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<SupplierRefreshToken> CreateRefreshTokenAsync(Guid contactId, string ipAddress)
    {
        var tokenBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);

        var refreshToken = new SupplierRefreshToken
        {
            Id = Guid.NewGuid(),
            SupplierContactId = contactId,
            Token = Convert.ToBase64String(tokenBytes),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierRefreshToken>().AddAsync(refreshToken);
        return refreshToken;
    }

    private static SupplierAuthResponse BuildAuthResponse(string accessToken, string refreshToken, SupplierContact contact)
    {
        return new SupplierAuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(60),
            Contact: new SupplierContactInfo(
                ContactId: contact.Id,
                SupplierId: contact.SupplierId,
                FullName: contact.FullName,
                Email: contact.Email,
                SupplierName: contact.Supplier?.Name ?? string.Empty,
                SupplierCode: contact.Supplier?.Code ?? string.Empty
            )
        );
    }

 
    public async Task<Guid> RegisterAsync(SupplierRegisterRequest request)
    {
        // Validate duplicate GSTIN
        bool gstinExists = await _uow.Repository<Supplier>().Query()
            .AnyAsync(s => s.GSTIN == request.GSTIN && s.IsActive);
        if (gstinExists)
            throw new BusinessRuleException($"A supplier with GSTIN '{request.GSTIN}' already exists.");

        // Validate duplicate PAN
        bool panExists = await _uow.Repository<Supplier>().Query()
            .AnyAsync(s => s.PAN == request.PAN && s.IsActive);
        if (panExists)
            throw new BusinessRuleException($"A supplier with PAN '{request.PAN}' already exists.");

        // Validate duplicate Email on Supplier
        bool emailExists = await _uow.Repository<Supplier>().Query()
            .AnyAsync(s => s.Email == request.Email && s.IsActive);
        if (emailExists)
            throw new BusinessRuleException($"A supplier with email '{request.Email}' already exists.");

        // Validate duplicate ContactEmail on SupplierContact
        bool contactEmailExists = await _uow.Repository<SupplierContact>().Query()
            .AnyAsync(c => c.Email == request.Email);
        if (contactEmailExists)
            throw new BusinessRuleException($"Email '{request.Email}' is already registered.");

        // Validate duplicate Phone on Supplier
        bool phoneExists = await _uow.Repository<Supplier>().Query()
            .AnyAsync(s => s.Phone == request.Phone && s.IsActive);
        if (phoneExists)
            throw new BusinessRuleException($"A supplier with phone '{request.Phone}' already exists.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            GSTIN = request.GSTIN,
            PAN = request.PAN,
            ContactPerson = request.ContactFullName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            LeadTimeDays = 0,
            PaymentTerms = PaymentTerms.Net30, // Default until review
            CreditLimit = 0,
            Rating = 0,
            IsActive = true,
            Status = SupplierStatus.Registered,
            RegistrationSource = RegistrationSource.SelfRegistered,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Supplier>().AddAsync(supplier);

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            FullName = request.ContactFullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Phone = request.Phone,
            JobTitle = "Primary Contact",
            IsActive = true,
            EmailVerified = false,
            EmailVerifyToken = new Random().Next(100000, 999999).ToString(), // random 6-digit OTP code
            EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<SupplierContact>().AddAsync(contact);
        await _uow.CommitAsync();

        var htmlBody = $@"
            <h2>Verify Your Email</h2>
            <p>Hi {request.ContactFullName},</p>
            <p>Thank you for registering. Your email verification code is: <strong>{contact.EmailVerifyToken}</strong></p>
            <p>This code expires in 15 minutes.</p>
        ";
        await _emailService.SendEmailAsync(request.Email, "SmartInventory - Email Verification", htmlBody);

        return contact.Id;
    }

 
    public async Task VerifyEmailAsync(SupplierVerifyEmailRequest request)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Email == request.Email && !c.EmailVerified);

        if (contact == null)
            throw new BusinessRuleException("Verification request is invalid or email is already verified.");

        // Check if locked due to too many failed attempts
        if (contact.OtpLockedUntil.HasValue && contact.OtpLockedUntil > DateTime.UtcNow)
        {
            var waitSeconds = (contact.OtpLockedUntil - DateTime.UtcNow).Value.TotalSeconds;
            throw new BusinessRuleException($"Too many failed attempts. Please wait {Math.Ceiling(waitSeconds)} seconds and try again.");
        }

        if (contact.EmailVerifyToken != request.Token)
        {
            contact.OtpRetryCount++;

            // Check if lockout should be applied
            if (contact.OtpRetryCount >= contact.OtpMaxRetries)
            {
                contact.OtpLockedUntil = DateTime.UtcNow.AddMinutes(15);
                contact.EmailVerifyToken = new Random().Next(100000, 999999).ToString();
                contact.EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(15);
                _uow.Repository<SupplierContact>().Update(contact);
                await _uow.CommitAsync();
                throw new BusinessRuleException($"Too many failed attempts. Account locked for 15 minutes.");
            }

            _uow.Repository<SupplierContact>().Update(contact);
            await _uow.CommitAsync();
            throw new BusinessRuleException($"Invalid verification token. {contact.OtpMaxRetries - contact.OtpRetryCount} attempts remaining.");
        }

        if (contact.EmailVerifyExpiresAt < DateTime.UtcNow)
        {
            // Regenerate OTP if expired
            contact.EmailVerifyToken = new Random().Next(100000, 999999).ToString();
            contact.EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(15);
            _uow.Repository<SupplierContact>().Update(contact);
            await _uow.CommitAsync();
            throw new BusinessRuleException("Verification token has expired. A new OTP has been sent.");
        }

        contact.EmailVerified = true;
        contact.EmailVerifyToken = null;
        contact.EmailVerifyExpiresAt = null;
        contact.OtpRetryCount = 0;
        contact.OtpLockedUntil = null;

        if (contact.Supplier != null && contact.Supplier.Status == SupplierStatus.Registered)
        {
            contact.Supplier.Status = SupplierStatus.PendingReview;
            _uow.Repository<Supplier>().Update(contact.Supplier);
        }

        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();

        // Send OTP Verified / Registration Pending Admin Review Email
        var htmlBody = $@"
            <h2>Email Verified Successfully</h2>
            <p>Hi {contact.FullName},</p>
            <p>Your email has been successfully verified.</p>
            <p>Your registration profile has now been submitted for Admin review. We will notify you once your application has been approved or if any further information is needed.</p>
        ";
        await _emailService.SendEmailAsync(contact.Email, "SmartInventory - Email Verified", htmlBody);
    }

    public async Task ResendOtpAsync(Guid contactId)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Id == contactId && !c.EmailVerified);

        if (contact == null)
            throw new BusinessRuleException("No pending email verification found.");

        // Check if locked
        if (contact.OtpLockedUntil.HasValue && contact.OtpLockedUntil > DateTime.UtcNow)
        {
            var waitSeconds = (contact.OtpLockedUntil - DateTime.UtcNow).Value.TotalSeconds;
            throw new BusinessRuleException($"Too many failed attempts. Please wait {Math.Ceiling(waitSeconds)} seconds and try again.");
        }

        // Check resend limit
        if (contact.OtpResendCount >= contact.OtpMaxResends)
        {
            throw new BusinessRuleException("Maximum OTP resend attempts exceeded. Please contact support.");
        }

        // Rate limit check - 1 minute cooldown
        if (contact.LastOtpSentAt.HasValue &&
            (DateTime.UtcNow - contact.LastOtpSentAt.Value).TotalMinutes < 1)
        {
            throw new BusinessRuleException("Please wait 1 minute before requesting another OTP.");
        }

        // Regenerate OTP
        contact.EmailVerifyToken = new Random().Next(100000, 999999).ToString();
        contact.EmailVerifyExpiresAt = DateTime.UtcNow.AddMinutes(15);
        contact.LastOtpSentAt = DateTime.UtcNow;
        contact.OtpResendCount++;

        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();
    }

    public async Task CompleteRegistrationAsync(SupplierCompleteRegistrationRequest request)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.InviteToken == request.InviteToken);

        if (supplier == null)
            throw new BusinessRuleException("Invalid invite token.");

        // Validate duplicate PAN at completion stage
        bool panExists = await _uow.Repository<Supplier>().Query()
            .AnyAsync(s => s.PAN == request.PAN && s.IsActive && s.Id != supplier.Id);
        if (panExists)
            throw new BusinessRuleException($"A supplier with PAN '{request.PAN}' already exists.");

        if (supplier.InviteTokenExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("Invite token has expired.");

        if (supplier.Status != SupplierStatus.InviteSent)
            throw new BusinessRuleException("Registration is already completed for this invite.");

        supplier.Status = SupplierStatus.PendingReview;
        supplier.InviteToken = null;
        supplier.InviteTokenExpiresAt = null;
        supplier.PAN = request.PAN;
        supplier.Address = request.Address;
        supplier.ContactPerson = request.ContactFullName;

        var contact = supplier.Contacts.FirstOrDefault();
        if (contact == null)
        {
            contact = new SupplierContact
            {
                Id = Guid.NewGuid(),
                SupplierId = supplier.Id,
                FullName = request.ContactFullName,
                Email = supplier.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                Phone = supplier.Phone,
                JobTitle = request.JobTitle,
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<SupplierContact>().AddAsync(contact);
        }
        else
        {
            contact.FullName = request.ContactFullName;
            contact.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
            contact.Phone = supplier.Phone ?? contact.Phone;
            contact.JobTitle = request.JobTitle;
            contact.EmailVerified = true;
            _uow.Repository<SupplierContact>().Update(contact);
        }

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();
    }


    public async Task ForgotPasswordAsync(string email)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Email == email && c.IsActive);

        if (contact == null)
            throw new BusinessRuleException("Email not found.");

        // Generate reset token
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddHours(1);

        contact.EmailVerifyToken = token;
        contact.EmailVerifyExpiresAt = expiresAt;

        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();

        // Send reset email via notification service
        var resetLink = $"https://app.smartware.com/supplier/reset-password?token={token}";
        await _notificationService.SendPasswordResetRequestAsync(contact.Id, email, resetLink, contact.FullName);
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.EmailVerifyToken == token && c.IsActive);

        if (contact == null)
            throw new BusinessRuleException("Invalid or expired reset token.");

        if (contact.EmailVerifyExpiresAt < DateTime.UtcNow)
        {
            contact.EmailVerifyToken = null;
            _uow.Repository<SupplierContact>().Update(contact);
            await _uow.CommitAsync();
            throw new BusinessRuleException("Reset token has expired.");
        }

        // Update password
        contact.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        contact.EmailVerifyToken = null;
        contact.EmailVerifyExpiresAt = null;

        // Revoke all refresh tokens
        var tokens = await _uow.Repository<SupplierRefreshToken>().Query()
            .Where(t => t.SupplierContactId == contact.Id && !t.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedReason = "Password reset";
            _uow.Repository<SupplierRefreshToken>().Update(t);
        }

        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();

        // Send success email
        await _notificationService.SendPasswordResetSuccessAsync(contact.Id, contact.Email, contact.FullName);
    }
}
