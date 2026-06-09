namespace SmartInventory.Core.Enums;

public enum AdjustmentReason
{
    CycleCount = 0, //Physical stock verification
    Damage = 1,
    Expiry = 2,
    Theft = 3,
    WriteOff = 4, //Business decision to permanently remove stock
    Found = 5,
    Correction = 6,
    Other = 7,
    LossInTransit = 8
}
