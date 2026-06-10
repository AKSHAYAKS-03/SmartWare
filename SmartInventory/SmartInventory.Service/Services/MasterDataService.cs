using System;
using System.Linq;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class MasterDataService : IMasterDataService
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cacheService;
    private const string CacheKey = "MasterData_Lookup";

    public MasterDataService(IUnitOfWork uow, ICacheService cacheService)
    {
        _uow = uow;
        _cacheService = cacheService;
    }

    public async Task<MasterDataResponseDto> GetMasterDataAsync()
    {
        // 1. Attempt to fetch from Redis/Memory Cache
        var cachedData = await _cacheService.GetAsync<MasterDataResponseDto>(CacheKey);
        if (cachedData != null)
        {
            return cachedData;
        }

        // 2. If Cache Miss, fetch from PostgreSQL using AsNoTracking for pure read speed
        var categories = await _uow.Repository<Category>().GetAllAsync(trackChanges: false);
        var warehouses = await _uow.Repository<Warehouse>().GetAllAsync(trackChanges: false);
        var roles = await _uow.Repository<Role>().GetAllAsync(trackChanges: false);

        var response = new MasterDataResponseDto
        {
            Categories = categories.Select(c => new LookupItemDto { Id = c.Id, Name = c.Name }).OrderBy(c => c.Name).ToList(),
            Warehouses = warehouses.Select(w => new LookupItemDto { Id = w.Id, Name = w.Name }).OrderBy(w => w.Name).ToList(),
            Roles = roles.Select(r => new LookupItemDto { Id = r.Id, Name = r.Name }).OrderBy(r => r.Name).ToList()
        };

        // 3. Save to Cache for 1 Hour (Enterprise optimization)
        await _cacheService.SetAsync(CacheKey, response, TimeSpan.FromHours(1));

        return response;
    }
}
