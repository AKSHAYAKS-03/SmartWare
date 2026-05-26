namespace SmartInventory.Core.Entities;

/// <summary>
/// JWT refresh token for token rotation.
/// </summary>
public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    // Foreign Keys
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
