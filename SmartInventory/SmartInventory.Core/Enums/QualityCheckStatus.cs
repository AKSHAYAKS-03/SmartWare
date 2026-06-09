namespace SmartInventory.Core.Enums;


// Quality check status applied to each line item during Goods Receipt (GRN) processing.
// Prevents damaged or non-conforming stock from entering the physical inventory

public enum QualityCheckStatus
{
    Pending = 0,

    Passed = 1,

    Failed = 2,

    PartiallyAccepted = 3
}
