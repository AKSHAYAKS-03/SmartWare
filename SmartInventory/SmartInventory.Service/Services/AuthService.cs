using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartInventory.Core;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;

namespace SmartInventory.Service.Services;

/// Full authentication service implementing JWT access tokens, BCrypt password hashing,
/// refresh-token rotation, and secure token revocation.
///
/// Security principles applied:
/// — Passwords are NEVER stored in plaintext. BCrypt with work factor 12 is used.
/// — JWT tokens embed userId, role, AND assignedWarehouseId so downstream services
///   can scope queries without additional DB lookups per request.
/// — Refresh tokens use cryptographically secure random bytes (not sequential GUIDs).
/// — Rotation: every use of a refresh token consumes it and issues a brand-new pair.

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly JWTsettings _jwtSettings;
    private readonly ITokenBlacklistService _blacklistService;

    public AuthService(IUnitOfWork uow, IOptions<JWTsettings> jwtOptions, ITokenBlacklistService blacklistService)
    {
        _uow = uow;
        _jwtSettings = jwtOptions.Value;
        _blacklistService = blacklistService;
    }


    // ─────────────────────────────────────────────────────────────────────────
    // SIGN IN
    // ─────────────────────────────────────────────────────────────────────────

    /// Authenticates a user by email + password and returns a token pair plus user profile.
    /// The JWT includes: sub (userId), role, warehouseId (primary assigned warehouse).
    
    public async Task<LoginResponseDto?> SignInAsync(LoginDto dto)
    {
        // 1. Load user with role and warehouse access records
        var user = await _uow.Repository<User>().Query()
            .Include(u => u.Role)
            .Include(u => u.WarehouseAccess)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

        if (user == null) return null;

        // Check if account has expired (enterprise auto-expiry logic)
        if (user.ExpiresAt.HasValue && user.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        // 2. Verify password using BCrypt (resilient fallback also in User.VerifyPassword)
        if (!user.VerifyPassword(dto.Password)) return null;

        // 3. Resolve primary warehouse for this user (manager/staff = first assigned warehouse)
        var primaryWarehouseId = user.WarehouseAccess
            .OrderBy(a => a.GrantedAt)
            .FirstOrDefault()?.WarehouseId;

        // 4. Issue access token + refresh token
        var accessToken = GenerateAccessToken(user, primaryWarehouseId);
        var (refreshToken, plainToken) = await CreateRefreshTokenAsync(user.Id);

        // 5. Update last login timestamp
        user.LastLogin = DateTime.UtcNow;
        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = plainToken,
            User = user.Adapt<UserResponseDto>()
        };
    }



    // ─────────────────────────────────────────────────────────────────────────
    // REFRESH TOKEN
    // ─────────────────────────────────────────────────────────────────────────

        /// Validates a refresh token and issues a new access + refresh token pair (rotation).
    /// The consumed token is immediately revoked — cannot be reused (replay attack protection).
    
    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        
        var storedToken = await _uow.Repository<RefreshToken>().Query()
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
            .Include(rt => rt.User)
                .ThenInclude(u => u.WarehouseAccess)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken);

        // Reject if token not found, already revoked, or expired
        if (storedToken == null) return null;
        if (storedToken.IsRevoked) return null;
        if (storedToken.ExpiresAt < DateTime.UtcNow) return null;
        if (!storedToken.User.IsActive) return null;
        if (storedToken.User.ExpiresAt.HasValue && storedToken.User.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        // Revoke the consumed token (rotation — each token is single-use)
        storedToken.IsRevoked = true;
        _uow.Repository<RefreshToken>().Update(storedToken);

        var user = storedToken.User;

        // Resolve primary warehouse
        var primaryWarehouseId = user.WarehouseAccess
            .OrderBy(a => a.GrantedAt)
            .FirstOrDefault()?.WarehouseId;

        // Issue new pair
        var newAccessToken = GenerateAccessToken(user, primaryWarehouseId);
        var (newRefreshToken, plainToken) = await CreateRefreshTokenAsync(user.Id);

        await _uow.CommitAsync();

        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = plainToken,
            User = user.Adapt<UserResponseDto>()
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REVOKE TOKEN (LOGOUT)
    // ─────────────────────────────────────────────────────────────────────────

        /// Revokes a refresh token — called on explicit logout.
    /// Silently succeeds if token is already revoked or not found.
    
    public async Task RevokeTokenAsync(string refreshToken)
    {
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        
        var storedToken = await _uow.Repository<RefreshToken>().Query()
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken);

        if (storedToken == null || storedToken.IsRevoked) return;

        storedToken.IsRevoked = true;
        _uow.Repository<RefreshToken>().Update(storedToken);
        await _uow.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CHANGE PASSWORD
    // ─────────────────────────────────────────────────────────────────────────

        /// Verifies the current password, then replaces it with a new BCrypt hash.
    /// Revokes ALL existing refresh tokens for the user on password change (force re-login).
    
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _uow.Repository<User>().Query().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null)
            throw new NotFoundException("User", userId);

        if (!user.VerifyPassword(dto.OldPassword))
            throw new BusinessRuleException("Current password is incorrect.");

        // Hash and save new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        _uow.Repository<User>().Update(user);

        // Revoke all existing refresh tokens — force re-authentication on all devices
        var activeTokens = await _uow.Repository<RefreshToken>().Query()
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            _uow.Repository<RefreshToken>().Update(token);
        }

        await _blacklistService.BlacklistUserAsync(userId);
        await _uow.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SET PASSWORD (INVITE FLOW)
    // ─────────────────────────────────────────────────────────────────────────

        /// Validates the one-time invite token and sets the employee's own password.
    /// On success:
    ///   — Password is hashed with BCrypt (work factor 12).
    ///   — InviteToken is cleared (single-use, cannot be replayed).
    ///   — IsPasswordSet = true (prevents re-use of this endpoint).
    ///   — Status transitions to Active (account is now fully operational).
    
    public async Task SetPasswordAsync(SetPasswordDto dto)
    {
        // Find user with a valid, unused invite token
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dto.Token)));
        var user = await _uow.Repository<User>().Query()
            .FirstOrDefaultAsync(u =>
                u.InviteToken == hashedToken &&
                u.InviteTokenExpiresAt > DateTime.UtcNow &&
                u.IsPasswordSet == false);

        if (user == null)
            throw new BusinessRuleException("Invalid or expired invitation link. Please contact your administrator.");

        // Set the employee's chosen password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        user.IsPasswordSet = true;

        // Invalidate the token — one-time use only
        user.InviteToken = null;
        user.InviteTokenExpiresAt = null;

        // Auto-activate — the invite link IS the vetting gate
        user.Status = UserStatus.Active;

        _uow.Repository<User>().Update(user);
        await _uow.CommitAsync();
    }


    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

        /// Generates a signed JWT access token with userId, role, and warehouseId claims.
    /// WarehouseId is critical — service-layer role scoping reads this claim directly.
    
    private string GenerateAccessToken(User user, Guid? warehouseId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name),
            new("role", user.Role.Name),
            new("userId", user.Id.ToString()),
            // WarehouseId claim — used by ICurrentUserService to scope all queries
            new("warehouseId", warehouseId?.ToString() ?? string.Empty)
        };

        // -- NEW: Inject True Permissions into the Token --
        if (user.Role?.Permissions != null)
        {
            foreach (var permission in user.Role.Permissions)
            {
                claims.Add(new Claim("Permission", permission));
            }
        }
        // ------------------------------------------------

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

        /// Creates and persists a cryptographically secure refresh token.
    /// Uses RandomNumberGenerator — not GUID — to produce a 64-byte hex string.
    
    private async Task<(RefreshToken Entity, string PlainToken)> CreateRefreshTokenAsync(Guid userId)
    {
        var tokenBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        
        var plainToken = Convert.ToBase64String(tokenBytes);
        var hashedToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<RefreshToken>().AddAsync(refreshToken);
        return (refreshToken, plainToken);
    }


}
