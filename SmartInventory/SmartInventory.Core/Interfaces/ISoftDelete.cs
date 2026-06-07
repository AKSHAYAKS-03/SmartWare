namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Defines a contract for entities that support soft deletion.
/// Instead of hard deleting records, we set IsActive to false to maintain referential integrity and audit trails.
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Gets or sets a value indicating whether the entity is active.
    /// If false, the entity is considered deleted.
    /// </summary>
    bool IsActive { get; set; }
}
