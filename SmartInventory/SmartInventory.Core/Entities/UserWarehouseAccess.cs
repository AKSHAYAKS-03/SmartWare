using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

public class UserWarehouseAccess : BaseEntity
{
    public AccessLevel AccessLevel { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;


    public Guid UserId { get; set; }
    public Guid WarehouseId { get; set; }


    public User User { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
