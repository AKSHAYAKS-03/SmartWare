namespace SmartInventory.Core.Interfaces;


// Instead of hard deleting records, we set IsActive to false to maintain referential integrity and audit trails.
public interface ISoftDelete
{
    
    bool IsActive { get; set; }
}
