namespace SmartInventory.Core.Enums;

/// <summary>
/// Official validation status of a physical warehouse asset.
/// </summary>
public enum WarehouseStatus
{
    PendingVerification = 0, // Freshly created, undergoing official business/facility evaluation
    Active = 1,              // Officially verified, approved, and operational
    Suspended = 2,           // Temporarily halted from operations due to audits or policy breaches
    Decommissioned = 3       // Permanently closed and legally inactive
}
