using System;
using System.Collections.Generic;

namespace SmartInventory.Core.DTOs;

public class LookupItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MasterDataResponseDto
{
    public IEnumerable<LookupItemDto> Categories { get; set; } = [];
    public IEnumerable<LookupItemDto> Warehouses { get; set; } = [];
    public IEnumerable<LookupItemDto> Roles { get; set; } = [];
}
