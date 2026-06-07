using MediatR;
using System;

namespace SmartInventory.Core.Events;

/// <summary>
/// Event published when a physical Bin Location exceeds its safe utilization threshold (e.g., 90%).
/// </summary>
public record BinCapacityThresholdReachedEvent(
    Guid BinId,
    string BinCode,
    decimal UtilizationPercentage
) : INotification;

/// <summary>
/// Event published when a user with sufficient privileges overrides a capacity constraint
/// (e.g., Zone mismatch or BinType mismatch) during an inventory movement.
/// </summary>
public record CapacityOverridePerformedEvent(
    Guid UserId,
    Guid BinId,
    string BinCode,
    Guid ProductId,
    string RuleBroken,
    string OverrideReason,
    DateTime Timestamp
) : INotification;
