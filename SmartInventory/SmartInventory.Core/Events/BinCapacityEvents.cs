using MediatR;
using System;

namespace SmartInventory.Core.Events;

/// Event published when a physical Bin Location exceeds its safe utilization threshold (e.g., 90%).
public record BinCapacityThresholdReachedEvent(
    Guid BinId,
    string BinCode,
    decimal UtilizationPercentage
) : INotification;

/// Event published when a user with sufficient privileges overrides a capacity constraint
/// (e.g., Zone mismatch or BinType mismatch) during an inventory movement.
public record CapacityOverridePerformedEvent(
    Guid UserId,
    Guid BinId,
    string BinCode,
    Guid ProductId,
    string RuleBroken,
    string OverrideReason,
    DateTime Timestamp
) : INotification;
