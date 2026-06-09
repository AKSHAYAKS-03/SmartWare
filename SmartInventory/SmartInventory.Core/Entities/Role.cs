using SmartInventory.Core.Attributes;
namespace SmartInventory.Core.Entities;
public class Role : BaseEntity
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public List<string> Permissions { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}
