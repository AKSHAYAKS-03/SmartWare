namespace SmartInventory.Core.Enums;

/// <summary>
/// Tracks the approval lifecycle for transfer transit variances.
/// Only set when a transfer is received with quantity shortfalls.
/// </summary>
public enum TransferVarianceResolutionStatus
{
    PendingApproval = 0,
    Resolved = 1,
    Rejected = 2
}
