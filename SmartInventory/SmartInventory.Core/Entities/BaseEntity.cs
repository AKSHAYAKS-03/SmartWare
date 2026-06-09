using SmartInventory.Core.Attributes;

namespace SmartInventory.Core.Entities;


public abstract class BaseEntity
{
    public Guid Id { get; set; }

    [Sortable]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Sortable]
    public DateTime? UpdatedAt { get; set; }
}
