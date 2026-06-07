namespace SmartInventory.Core.Enums;

public enum TransferStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Dispatched = 3,
    InTransit = 4,
    Received = 5,
    Cancelled = 6,
    ReceivedWithVariance = 7
}
