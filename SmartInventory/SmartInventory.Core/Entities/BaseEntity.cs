namespace SmartInventory.Core.Entities;

/// <summary>
/// Base entity with common fields for all entities.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
