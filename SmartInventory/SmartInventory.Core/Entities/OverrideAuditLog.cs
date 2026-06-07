using System;

namespace SmartInventory.Core.Entities;

public class OverrideAuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // e.g. "ZoneMismatch", "BinTypeMismatch"
    public string RuleBroken { get; set; } = string.Empty;
    
    public string OverrideReason { get; set; } = string.Empty;
    
    public Guid? SourceBinId { get; set; }
    public Guid? TargetBinId { get; set; }
    public Guid? ProductId { get; set; }
    
    // Serialized state info for forensic audits
    public string? PreviousStateJson { get; set; }
    public string? NewStateJson { get; set; }

    // Navigation (Optional but good for EF)
    public User User { get; set; } = null!;
    public BinLocation? TargetBin { get; set; }
    public Product? Product { get; set; }
}
