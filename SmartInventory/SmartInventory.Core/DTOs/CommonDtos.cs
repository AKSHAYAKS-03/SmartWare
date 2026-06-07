using System.Collections.Generic;

namespace SmartInventory.Core.DTOs;

public class QueryParameters
{
    public int Page { get; set; } = 1;
    
    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 100 ? 100 : value;
    }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortDir { get; set; } = "desc";
    public bool SkipTotalCount { get; set; } = false;
}

public class PagedResult<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => TotalCount == -1 ? -1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    // If Count was skipped, guess NextPage based on whether a full page was returned
    public bool HasNextPage => TotalCount == -1 ? Data.Count() == PageSize : Page < TotalPages;
    
    public bool HasPreviousPage => Page > 1;
}
