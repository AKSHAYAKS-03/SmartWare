using System;
using System.Collections.Generic;

namespace SmartInventory.Core.DTOs;

/// <summary>
/// A minimal DTO for dropdowns and lookup lists to minimize payload size over the network.
/// </summary>
public class LookupItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A consolidated payload containing all highly static master data required by the Frontend application upon startup.
/// </summary>
public class MasterDataResponseDto
{
    public IEnumerable<LookupItemDto> Categories { get; set; } = [];
    public IEnumerable<LookupItemDto> Warehouses { get; set; } = [];
    public IEnumerable<LookupItemDto> Roles { get; set; } = [];
}
