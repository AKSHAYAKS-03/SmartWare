using System;
using SmartInventory.Core.Attributes;
using System.Collections.Generic;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Physical warehouse location with official certification and capacity parameters.
/// </summary>
public class Warehouse : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = string.Empty; // Mandatory per Indian business reqs
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;

    // Capacity Limits
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }

    // Practical Business Contacts
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }

    // Enterprise Official Validation & Business Compliance
    public string? GSTIN { get; set; }              // Optional GSTIN support
    public string? RegistrationNumber { get; set; } // Optional official business registration / permit number
    [Sortable]
    public WarehouseStatus Status { get; set; } = WarehouseStatus.PendingVerification;
    // Foreign Keys
    public Guid? ManagerId { get; set; }

    // Navigation
    public User? Manager { get; set; }
    public ICollection<WarehouseZone> Zones { get; set; } = [];
    public ICollection<UserWarehouseAccess> UserAccess { get; set; } = [];
    public ICollection<StockLevel> StockLevels { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<AlertConfiguration> AlertConfigurations { get; set; } = [];
}
