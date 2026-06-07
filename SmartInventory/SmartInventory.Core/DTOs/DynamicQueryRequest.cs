using System.Collections.Generic;

namespace SmartInventory.Core.DTOs;

public class DynamicQueryRequest
{
    public int Page { get; set; } = 1;
    
    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 100 ? 100 : value;
    }
    public string? GlobalSearch { get; set; }
    public List<FilterCriteria> Filters { get; set; } = [];
    public List<SortCriteria> Sorts { get; set; } = [];
    public bool SkipTotalCount { get; set; } = false;
}

public class FilterCriteria
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq"; // Valid values: eq, neq, gt, lt, contains
    public string? Value { get; set; } // Passed as string from JSON, safely parsed at runtime
}

public class SortCriteria
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc"; // Valid values: asc, desc
}
