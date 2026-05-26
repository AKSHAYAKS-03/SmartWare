using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Physical warehouse location with official certification and capacity parameters.
/// </summary>
public class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;

    // Enterprise Official Validation & Business Compliance
    public string? TaxIdentifier { get; set; }      // Corporate Tax Registration ID (e.g. VAT/GSTIN)
    public string? RegistrationNumber { get; set; } // Official government business registration / permit number
    public WarehouseStatus Status { get; set; } = WarehouseStatus.PendingVerification;
    public Guid? ApprovedById { get; set; }          // The Admin user who verified and certified this warehouse
    public DateTime? ApprovedAt { get; set; }

    // Foreign Keys
    public Guid? ManagerId { get; set; }

    // Navigation
    public User? Manager { get; set; }
    public User? ApprovedBy { get; set; } // Admin who signed off the warehouse
    public ICollection<WarehouseZone> Zones { get; set; } = [];
    public ICollection<UserWarehouseAccess> UserAccess { get; set; } = [];
    public ICollection<StockLevel> StockLevels { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<AlertConfiguration> AlertConfigurations { get; set; } = [];
}
