using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region Warehouse DTOs
public class WarehouseCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? TaxIdentifier { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WarehouseUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? TaxIdentifier { get; set; }
    public string? RegistrationNumber { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; }
}

public class WarehouseResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? TaxIdentifier { get; set; }
    public string? RegistrationNumber { get; set; }
    public WarehouseStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
#endregion

#region WarehouseZone DTOs
public class ZoneCreateDto
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ZoneUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; }
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
}
#endregion

#region BinLocation DTOs
public class BinLocationCreateDto
{
    public Guid ZoneId { get; set; }
    public string Aisle { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BinLocationUpdateDto
{
    public string Aisle { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public bool IsActive { get; set; }
}

public class BinLocationResponseDto
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Aisle { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Code => $"{Aisle}-{Rack}-{Bin}";
    public string? Barcode { get; set; }
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
