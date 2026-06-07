namespace SmartInventory.Core.Enums;

/// <summary>
/// Quality check status applied to each line item during Goods Receipt (GRN) processing.
/// Prevents damaged or non-conforming stock from entering the physical inventory
/// (Final_Plan GRN Quality Check feature).
/// </summary>
public enum QualityCheckStatus
{
    /// <summary>Quality check not yet performed — default state on GRN creation.</summary>
    Pending = 0,

    /// <summary>Item passed all quality criteria — accepted into inventory.</summary>
    Passed = 1,

    /// <summary>Item failed quality criteria — rejected and not added to inventory.</summary>
    Failed = 2,

    /// <summary>Part of the quantity passed, part failed — partial acceptance.</summary>
    PartiallyAccepted = 3
}
