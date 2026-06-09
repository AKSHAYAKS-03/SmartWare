using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region Warehouse DTOs
public class WarehouseCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    
    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    
    public string? GSTIN { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}

public class WarehouseUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }

    public string? GSTIN { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; }
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}

public class WarehouseResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }

    public string? GSTIN { get; set; }
    public string? RegistrationNumber { get; set; }
    public WarehouseStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}
#endregion

#region WarehouseZone DTOs
public class ZoneCreateDto
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }

    // When true, the capacity engine enforces volume and weight limits on bins in this zone.
    // Default is false to preserve backward compatibility. Enable once bins have MaxVolumeCm3 / MaxWeightKg configured.
    public bool IsCapacityEnforced { get; set; } = false;
    public bool IsActive { get; set; } = true;
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}

public class ZoneUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; }
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}

public class ZoneResponseDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public string ZoneTypeName => ZoneType.ToString();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }
}
#endregion

#region BinLocation DTOs
public class BinLocationCreateDto
{
    public Guid ZoneId { get; set; }
    public BinType BinType { get; set; } = BinType.Standard;
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class BinLocationUpdateDto
{
    public BinType BinType { get; set; } = BinType.Standard;
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; } = 0;
    public bool IsActive { get; set; }
}

public class BinLocationResponseDto
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string BinCode { get; set; } = string.Empty;
    public string Code => BinCode;
    public string? Barcode { get; set; }

    public BinType BinType { get; set; }
    public string BinTypeName => BinType.ToString();
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }

    // Live utilization (materialized columns — O(1) reads)
    public decimal UtilizedVolumeCm3 { get; set; }
    public decimal UtilizedWeightKg { get; set; }

    // Computed utilization percentages
    public decimal VolumeUtilizationPct => MaxVolumeCm3 > 0 ? Math.Round((UtilizedVolumeCm3 / MaxVolumeCm3) * 100, 1) : 0;
    public decimal WeightUtilizationPct => MaxWeightKg > 0 ? Math.Round((UtilizedWeightKg / MaxWeightKg) * 100, 1) : 0;

   //True when either MaxVolumeCm3 or MaxWeightKg is configured (> 0).</summary>
    public bool IsCapacityConfigured => MaxVolumeCm3 > 0 || MaxWeightKg > 0;

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion

#region UserWarehouseAccess DTOs
public class UserWarehouseAccessCreateDto
{
    public Guid UserId { get; set; }
    public Guid WarehouseId { get; set; }
    public AccessLevel AccessLevel { get; set; }
}

public class UserWarehouseAccessResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public AccessLevel AccessLevel { get; set; }
    public string AccessLevelName => AccessLevel.ToString();
    public DateTime GrantedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion

#region Capacity Summary DTOs
public class CapacitySummaryDto
{
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    
    // Volume
    public decimal TotalVolumeCm3 { get; set; }
    public decimal UtilizedVolumeCm3 { get; set; }
    public decimal RemainingVolumeCm3 => TotalVolumeCm3 - UtilizedVolumeCm3;
    public decimal VolumeUtilizationPct => TotalVolumeCm3 > 0 ? Math.Round((UtilizedVolumeCm3 / TotalVolumeCm3) * 100, 1) : 0;
    
    // Weight
    public decimal TotalWeightKg { get; set; }
    public decimal UtilizedWeightKg { get; set; }
    public decimal RemainingWeightKg => TotalWeightKg - UtilizedWeightKg;
    public decimal WeightUtilizationPct => TotalWeightKg > 0 ? Math.Round((UtilizedWeightKg / TotalWeightKg) * 100, 1) : 0;
    
    public int CapacityEnforcedBinCount { get; set; }
}
#endregion
