namespace SmartInventory.Core.Enums;

/// <summary>
/// Reason codes for inventory shrinkage events recorded in stock adjustments.
/// Enables management to identify operational problems and warehouse risks
/// through shrinkage reports (Final_Plan feature).
/// </summary>
public enum ShrinkageReason
{
    /// <summary>Inventory lost due to internal or external theft.</summary>
    Theft = 0,

    /// <summary>Inventory rendered unusable due to physical damage.</summary>
    Damage = 1,

    /// <summary>Inventory written off due to passing the expiry or use-by date.</summary>
    Expiry = 2,

    /// <summary>Errors in data entry, receiving counts, or pick/pack processes.</summary>
    AdministrativeError = 3,

    /// <summary>Loss from normal handling and transit breakage.</summary>
    HandlingLoss = 4,

    /// <summary>Any other shrinkage reason not covered by the above categories.</summary>
    Other = 5
}
