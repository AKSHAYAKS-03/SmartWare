namespace SmartInventory.Core.Enums;

/// <summary>
/// Classifies the type of product in the inventory.
/// Helps businesses prioritise handling and safety-stock requirements.
/// Raw materials stopping production have a higher impact than finished-goods low stock.
/// </summary>
public enum ProductType
{
    /// <summary>Unprocessed input materials (e.g. steel, cotton, chemicals).</summary>
    Raw = 0,

    /// <summary>Partially processed items that are mid-production cycle.</summary>
    WorkInProgress = 1,

    /// <summary>Completed goods ready for sale or dispatch.</summary>
    Finished = 2,

    /// <summary>Maintenance, Repair &amp; Operations consumables (e.g. machine parts, lubricants).</summary>
    MRO = 3
}
