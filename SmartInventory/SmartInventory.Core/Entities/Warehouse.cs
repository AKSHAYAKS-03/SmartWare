using System;
using SmartInventory.Core.Attributes;
using System.Collections.Generic;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

public class Warehouse : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = null!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;


    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }


    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }


    public string? GSTIN { get; set; }             
    public string? RegistrationNumber { get; set; }
    [Sortable]
    public WarehouseStatus Status { get; set; } = WarehouseStatus.PendingVerification;

    public Guid? ManagerId { get; set; }


    public User? Manager { get; set; }
    public ICollection<WarehouseZone> Zones { get; set; } = [];
    public ICollection<UserWarehouseAccess> UserAccess { get; set; } = [];
    public ICollection<StockLevel> StockLevels { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<AlertConfiguration> AlertConfigurations { get; set; } = [];
}
