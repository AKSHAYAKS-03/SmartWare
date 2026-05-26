namespace SmartInventory.Core.Entities;

/// <summary>
/// Tracks auto-incrementing sequence numbers for entities (PO, GRN, TRF, ADJ, etc.).
/// Updated atomically via PostgreSQL function.
/// </summary>
public class SequenceCounter : BaseEntity
{
    public string EntityName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int CurrentValue { get; set; } = 0;
}
