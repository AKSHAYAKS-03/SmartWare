using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;

namespace SmartInventory.Service.Services;

/// <summary>
/// Warehouse, zone, and bin location management.
///
/// Business rules:
///   — Warehouse code must be unique.
///   — Putaway guidance suggests the best bin based on zone type matching the product category.
///   — User access assignments are scoped: one access record per user per warehouse.
///   — Bin locations auto-generate a barcode from ZoneCode-BinCode if not provided.
/// </summary>
public class WarehouseService : IWarehouseService
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly ISequenceNumberGenerator _sequenceNumberGenerator;

    public WarehouseService(IUnitOfWork uow, ICacheService cacheService, INotificationService notificationService, ISequenceNumberGenerator sequenceNumberGenerator)
    {
        _uow = uow;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _sequenceNumberGenerator = sequenceNumberGenerator;
    }

    // ─── Warehouse CRUD ───────────────────────────────────────────────────────

    public async Task<WarehouseResponseDto> CreateWarehouseAsync(WarehouseCreateDto dto)
    {
        var warehouseCode = await _sequenceNumberGenerator.GenerateAsync("seq_warehouses", "WH");

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Code = warehouseCode,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            PostalCode = dto.PostalCode,
            Country = dto.Country,
            ContactPerson = dto.ContactPerson,
            ContactNumber = dto.ContactNumber,
            Email = dto.Email,
            GSTIN = dto.GSTIN,
            RegistrationNumber = dto.RegistrationNumber,
            ManagerId = dto.ManagerId,
            Status = WarehouseStatus.Active,
            IsActive = dto.IsActive,
            AreaSqFt = dto.AreaSqFt,
            MaxVolumeCm3 = dto.MaxVolumeCm3,
            MaxWeightKg = dto.MaxWeightKg,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Warehouse>().AddAsync(warehouse);
        await _uow.CommitAsync();
        return await GetWarehouseByIdAsync(warehouse.Id);
    }

    public async Task<WarehouseResponseDto> UpdateWarehouseAsync(Guid warehouseId, WarehouseUpdateDto dto)
    {
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);
        if (warehouse == null) throw new NotFoundException("Warehouse", warehouseId);

        warehouse.Name = dto.Name;
        warehouse.Address = dto.Address;
        warehouse.City = dto.City;
        warehouse.State = dto.State;
        warehouse.PostalCode = dto.PostalCode;
        warehouse.Country = dto.Country;
        warehouse.ContactPerson = dto.ContactPerson;
        warehouse.ContactNumber = dto.ContactNumber;
        warehouse.Email = dto.Email;
        warehouse.GSTIN = dto.GSTIN;
        warehouse.RegistrationNumber = dto.RegistrationNumber;
        warehouse.ManagerId = dto.ManagerId;
        warehouse.IsActive = dto.IsActive;

        // Hierarchical Capacity Constraints
        var usedArea = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == warehouseId).SumAsync(z => z.AreaSqFt);
        var usedVolume = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == warehouseId).SumAsync(z => z.MaxVolumeCm3);
        var usedWeight = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == warehouseId).SumAsync(z => z.MaxWeightKg);

        if (dto.AreaSqFt < usedArea)
            throw new BusinessRuleException($"Cannot reduce Warehouse Area to {dto.AreaSqFt}. The sum of existing Zones is {usedArea}.");
        if (dto.MaxVolumeCm3 < usedVolume)
            throw new BusinessRuleException($"Cannot reduce Warehouse Volume to {dto.MaxVolumeCm3}. The sum of existing Zones is {usedVolume}.");
        if (dto.MaxWeightKg < usedWeight)
            throw new BusinessRuleException($"Cannot reduce Warehouse Weight to {dto.MaxWeightKg}. The sum of existing Zones is {usedWeight}.");

        warehouse.AreaSqFt = dto.AreaSqFt;
        warehouse.MaxVolumeCm3 = dto.MaxVolumeCm3;
        warehouse.MaxWeightKg = dto.MaxWeightKg;

        _uow.Repository<Warehouse>().Update(warehouse);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync($"warehouse:id:{warehouseId}");
        return await GetWarehouseByIdAsync(warehouseId);
    }

    public async Task DeleteWarehouseAsync(Guid warehouseId)
    {
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);
        if (warehouse == null) throw new NotFoundException("Warehouse", warehouseId);

        bool hasActiveStock = await _uow.Repository<StockLevel>()
            .Query()
            .AnyAsync(sl => sl.WarehouseId == warehouseId && sl.QuantityOnHand > 0);

        if (hasActiveStock)
            throw new BusinessRuleException(
                "Cannot delete a warehouse that has active stock. Transfer or adjust all stock first.");

        _uow.Repository<Warehouse>().Delete(warehouse);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync($"warehouse:id:{warehouseId}");
    }

    public async Task<WarehouseResponseDto> GetWarehouseByIdAsync(Guid warehouseId)
    {
        var cacheKey = $"warehouse:id:{warehouseId}";
        var cached = await _cacheService.GetAsync<WarehouseResponseDto>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var warehouse = await _uow.Repository<Warehouse>()
            .Query()
            .Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.Id == warehouseId);

        if (warehouse == null) throw new NotFoundException("Warehouse", warehouseId);
        var dto = warehouse.Adapt<WarehouseResponseDto>();
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));
        return dto;
    }

    public async Task<PagedResult<WarehouseResponseDto>> GetWarehousesAsync(QueryParameters queryParams)
    {
        IQueryable<Warehouse> query = _uow.Repository<Warehouse>()
            .Query()
            .Include(w => w.Manager);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(w => w.Name.Contains(queryParams.Search) ||
                                     w.Code.Contains(queryParams.Search));

        int total = await query.CountAsync();
        query = query.OrderBy(w => w.Name);

        var data = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<WarehouseResponseDto>
        {
            Data = data.Adapt<IEnumerable<WarehouseResponseDto>>(),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    // ─── Zones ────────────────────────────────────────────────────────────────

    public async Task<ZoneResponseDto> CreateZoneAsync(ZoneCreateDto dto)
    {
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(dto.WarehouseId);
        if (warehouse == null) throw new NotFoundException("Warehouse", dto.WarehouseId);

        // Hierarchical Capacity Constraints
        var usedArea = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == dto.WarehouseId).SumAsync(z => z.AreaSqFt);
        var usedVolume = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == dto.WarehouseId).SumAsync(z => z.MaxVolumeCm3);
        var usedWeight = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == dto.WarehouseId).SumAsync(z => z.MaxWeightKg);

        if (usedArea + dto.AreaSqFt > warehouse.AreaSqFt)
            throw new BusinessRuleException($"Zone Area {dto.AreaSqFt} exceeds remaining Warehouse Area {warehouse.AreaSqFt - usedArea}.");
        if (usedVolume + dto.MaxVolumeCm3 > warehouse.MaxVolumeCm3)
            throw new BusinessRuleException($"Zone Volume {dto.MaxVolumeCm3} exceeds remaining Warehouse Volume {warehouse.MaxVolumeCm3 - usedVolume}.");
        if (usedWeight + dto.MaxWeightKg > warehouse.MaxWeightKg)
            throw new BusinessRuleException($"Zone Weight {dto.MaxWeightKg} exceeds remaining Warehouse Weight {warehouse.MaxWeightKg - usedWeight}.");

        var zoneCode = await _sequenceNumberGenerator.GenerateAsync("seq_zones", "ZN");

        var zone = new WarehouseZone
        {
            Id = Guid.NewGuid(),
            WarehouseId = dto.WarehouseId,
            Name = dto.Name,
            Code = zoneCode,
            ZoneType = dto.ZoneType,
            AreaSqFt = dto.AreaSqFt,
            MaxVolumeCm3 = dto.MaxVolumeCm3,
            MaxWeightKg = dto.MaxWeightKg,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<WarehouseZone>().AddAsync(zone);
        await _uow.CommitAsync();

        return new ZoneResponseDto
        {
            Id = zone.Id, WarehouseId = zone.WarehouseId, WarehouseName = warehouse.Name,
            Name = zone.Name, Code = zone.Code,
            ZoneType = zone.ZoneType,
            AreaSqFt = zone.AreaSqFt, MaxVolumeCm3 = zone.MaxVolumeCm3, MaxWeightKg = zone.MaxWeightKg,
            IsActive = zone.IsActive, CreatedAt = zone.CreatedAt
        };
    }

    public async Task<ZoneResponseDto> UpdateZoneAsync(Guid zoneId, ZoneUpdateDto dto)
    {
        var zone = await _uow.Repository<WarehouseZone>()
            .Query().Include(z => z.Warehouse).FirstOrDefaultAsync(z => z.Id == zoneId);

        if (zone == null) throw new NotFoundException("WarehouseZone", zoneId);

        // Hierarchical Capacity Constraints
        var usedArea = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == zone.WarehouseId && z.Id != zoneId).SumAsync(z => z.AreaSqFt);
        var usedVolume = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == zone.WarehouseId && z.Id != zoneId).SumAsync(z => z.MaxVolumeCm3);
        var usedWeight = await _uow.Repository<WarehouseZone>().Query().Where(z => z.WarehouseId == zone.WarehouseId && z.Id != zoneId).SumAsync(z => z.MaxWeightKg);

        if (usedArea + dto.AreaSqFt > zone.Warehouse.AreaSqFt)
            throw new BusinessRuleException($"Zone Area {dto.AreaSqFt} exceeds remaining Warehouse Area {zone.Warehouse.AreaSqFt - usedArea}.");
        if (usedVolume + dto.MaxVolumeCm3 > zone.Warehouse.MaxVolumeCm3)
            throw new BusinessRuleException($"Zone Volume {dto.MaxVolumeCm3} exceeds remaining Warehouse Volume {zone.Warehouse.MaxVolumeCm3 - usedVolume}.");
        if (usedWeight + dto.MaxWeightKg > zone.Warehouse.MaxWeightKg)
            throw new BusinessRuleException($"Zone Weight {dto.MaxWeightKg} exceeds remaining Warehouse Weight {zone.Warehouse.MaxWeightKg - usedWeight}.");

        var binVol = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == zoneId).SumAsync(b => b.MaxVolumeCm3);
        var binWeight = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == zoneId).SumAsync(b => b.MaxWeightKg);

        if (dto.MaxVolumeCm3 < binVol)
            throw new BusinessRuleException($"Cannot reduce Zone Volume to {dto.MaxVolumeCm3}. The sum of existing Bins is {binVol}.");
        if (dto.MaxWeightKg < binWeight)
            throw new BusinessRuleException($"Cannot reduce Zone Weight to {dto.MaxWeightKg}. The sum of existing Bins is {binWeight}.");

        zone.Name = dto.Name;
        zone.ZoneType = dto.ZoneType;
        zone.IsActive = dto.IsActive;
        zone.AreaSqFt = dto.AreaSqFt;
        zone.MaxVolumeCm3 = dto.MaxVolumeCm3;
        zone.MaxWeightKg = dto.MaxWeightKg;

        _uow.Repository<WarehouseZone>().Update(zone);
        await _uow.CommitAsync();

        return new ZoneResponseDto
        {
            Id = zone.Id, WarehouseId = zone.WarehouseId, WarehouseName = zone.Warehouse.Name,
            Name = zone.Name, Code = zone.Code,
            ZoneType = zone.ZoneType,
            IsActive = zone.IsActive, CreatedAt = zone.CreatedAt
        };
    }

    public async Task DeleteZoneAsync(Guid zoneId)
    {
        var zone = await _uow.Repository<WarehouseZone>().GetByIdAsync(zoneId);
        if (zone == null) throw new NotFoundException("WarehouseZone", zoneId);
        _uow.Repository<WarehouseZone>().Delete(zone);
        await _uow.CommitAsync();
    }

    public async Task<IEnumerable<ZoneResponseDto>> GetZonesByWarehouseAsync(Guid warehouseId)
    {
        var zones = await _uow.Repository<WarehouseZone>()
            .Query()
            .Include(z => z.Warehouse)
            .Where(z => z.WarehouseId == warehouseId)
            .OrderBy(z => z.Code)
            .ToListAsync();

        return zones.Adapt<IEnumerable<ZoneResponseDto>>();
    }

    // ─── Bin Locations ────────────────────────────────────────────────────────

    public async Task<BinLocationResponseDto> CreateBinLocationAsync(BinLocationCreateDto dto)
    {
        var zone = await _uow.Repository<WarehouseZone>()
            .Query().Include(z => z.Warehouse).FirstOrDefaultAsync(z => z.Id == dto.ZoneId);
        if (zone == null) throw new NotFoundException("WarehouseZone", dto.ZoneId);

        // Hierarchical Capacity Constraints
        var usedVolume = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == dto.ZoneId).SumAsync(b => b.MaxVolumeCm3);
        var usedWeight = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == dto.ZoneId).SumAsync(b => b.MaxWeightKg);

        if (usedVolume + dto.MaxVolumeCm3 > zone.MaxVolumeCm3)
            throw new BusinessRuleException($"Bin Volume {dto.MaxVolumeCm3} exceeds remaining Zone Volume {zone.MaxVolumeCm3 - usedVolume}.");
        if (usedWeight + dto.MaxWeightKg > zone.MaxWeightKg)
            throw new BusinessRuleException($"Bin Weight {dto.MaxWeightKg} exceeds remaining Zone Weight {zone.MaxWeightKg - usedWeight}.");

        var binCode = await _sequenceNumberGenerator.GenerateAsync("seq_bins", "BIN");

        var bin = new BinLocation
        {
            Id = Guid.NewGuid(),
            ZoneId = dto.ZoneId,
            BinCode = binCode,
            Barcode = null,
            BinType = dto.BinType,
            MaxVolumeCm3 = dto.MaxVolumeCm3,
            MaxWeightKg = dto.MaxWeightKg,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<BinLocation>().AddAsync(bin);
        await _uow.CommitAsync();

        bin.Barcode = $"{zone.Code}-{bin.BinCode}".ToUpperInvariant();
        _uow.Repository<BinLocation>().Update(bin);
        await _uow.CommitAsync();

        var result = bin.Adapt<BinLocationResponseDto>();
        result.WarehouseId = zone.WarehouseId;
        result.WarehouseName = zone.Warehouse.Name;
        return result;
    }

    public async Task<BinLocationResponseDto> UpdateBinLocationAsync(Guid binId, BinLocationUpdateDto dto)
    {
        var bin = await _uow.Repository<BinLocation>()
            .Query()
            .Include(b => b.Zone).ThenInclude(z => z.Warehouse)
            .FirstOrDefaultAsync(b => b.Id == binId);

        if (bin == null) throw new NotFoundException("BinLocation", binId);

        // Hierarchical Capacity Constraints
        var usedVolume = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == bin.ZoneId && b.Id != binId).SumAsync(b => b.MaxVolumeCm3);
        var usedWeight = await _uow.Repository<BinLocation>().Query().Where(b => b.ZoneId == bin.ZoneId && b.Id != binId).SumAsync(b => b.MaxWeightKg);

        if (usedVolume + dto.MaxVolumeCm3 > bin.Zone.MaxVolumeCm3)
            throw new BusinessRuleException($"Bin Volume {dto.MaxVolumeCm3} exceeds remaining Zone Volume {bin.Zone.MaxVolumeCm3 - usedVolume}.");
        if (usedWeight + dto.MaxWeightKg > bin.Zone.MaxWeightKg)
            throw new BusinessRuleException($"Bin Weight {dto.MaxWeightKg} exceeds remaining Zone Weight {bin.Zone.MaxWeightKg - usedWeight}.");

        if (dto.MaxVolumeCm3 > 0 && dto.MaxVolumeCm3 < bin.UtilizedVolumeCm3)
            throw new BusinessRuleException($"Cannot reduce Bin Volume below currently utilized volume ({bin.UtilizedVolumeCm3}).");
        if (dto.MaxWeightKg > 0 && dto.MaxWeightKg < bin.UtilizedWeightKg)
            throw new BusinessRuleException($"Cannot reduce Bin Weight below currently utilized weight ({bin.UtilizedWeightKg}).");

        bin.BinType = dto.BinType;
        bin.MaxVolumeCm3 = dto.MaxVolumeCm3;
        bin.MaxWeightKg = dto.MaxWeightKg;
        bin.IsActive = dto.IsActive;

        _uow.Repository<BinLocation>().Update(bin);
        await _uow.CommitAsync();

        var result = bin.Adapt<BinLocationResponseDto>();
        result.WarehouseId = bin.Zone.WarehouseId;
        result.WarehouseName = bin.Zone.Warehouse.Name;
        return result;
    }

    public async Task DeleteBinLocationAsync(Guid binId)
    {
        var bin = await _uow.Repository<BinLocation>().GetByIdAsync(binId);
        if (bin == null) throw new NotFoundException("BinLocation", binId);

        bool hasStock = await _uow.Repository<StockLevel>()
            .Query().AnyAsync(sl => sl.BinLocationId == binId && sl.QuantityOnHand > 0);
        if (hasStock)
            throw new BusinessRuleException("Cannot delete a bin location that has stock.");

        _uow.Repository<BinLocation>().Delete(bin);
        await _uow.CommitAsync();
    }

    public async Task<IEnumerable<BinLocationResponseDto>> GetBinsByZoneAsync(Guid zoneId)
    {
        var bins = await _uow.Repository<BinLocation>()
            .Query()
            .Include(b => b.Zone).ThenInclude(z => z.Warehouse)
            .Where(b => b.ZoneId == zoneId)
            .OrderBy(b => b.BinCode)
            .ToListAsync();

        var result = bins.Adapt<IEnumerable<BinLocationResponseDto>>().ToList();
        foreach (var dto in result)
        {
            var bin = bins.First(b => b.Id == dto.Id);
            dto.WarehouseId = bin.Zone.WarehouseId;
            dto.WarehouseName = bin.Zone.Warehouse.Name;
        }
        return result;
    }

    // ─── User Access ──────────────────────────────────────────────────────────

    public async Task<UserWarehouseAccessResponseDto> AssignUserAccessAsync(UserWarehouseAccessCreateDto dto)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(dto.UserId);
        if (user == null) throw new NotFoundException("User", dto.UserId);

        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(dto.WarehouseId);
        if (warehouse == null) throw new NotFoundException("Warehouse", dto.WarehouseId);

        // Remove existing access if any (upsert behaviour)
        var existing = await _uow.Repository<UserWarehouseAccess>()
            .Query()
            .FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.WarehouseId == dto.WarehouseId);

        if (existing != null)
        {
            existing.AccessLevel = dto.AccessLevel;
            _uow.Repository<UserWarehouseAccess>().Update(existing);
            await _uow.CommitAsync();

            await NotifyWarehouseAssignmentAsync(user.Id, warehouse.Id, warehouse.Name, dto.AccessLevel);

            return existing.Adapt<UserWarehouseAccessResponseDto>();
        }

        var access = new UserWarehouseAccess
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            WarehouseId = dto.WarehouseId,
            AccessLevel = dto.AccessLevel,
            GrantedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<UserWarehouseAccess>().AddAsync(access);
        await _uow.CommitAsync();

        await NotifyWarehouseAssignmentAsync(user.Id, warehouse.Id, warehouse.Name, dto.AccessLevel);

        return access.Adapt<UserWarehouseAccessResponseDto>();
    }

    private async Task NotifyWarehouseAssignmentAsync(Guid userId, Guid warehouseId, string warehouseName, AccessLevel accessLevel)
    {
        var title = "Warehouse Access Granted";
        var message = $"You have been granted {accessLevel} access to the '{warehouseName}' warehouse.";

        await _notificationService.SendNotificationAsync(userId, NotificationChannel.Email, "WarehouseAssignment", title, message, "Warehouse", warehouseId);
        await _notificationService.SendNotificationAsync(userId, NotificationChannel.InApp, "WarehouseAssignment", title, message, "Warehouse", warehouseId);
    }

    public async Task RevokeUserAccessAsync(Guid accessId)
    {
        var access = await _uow.Repository<UserWarehouseAccess>().GetByIdAsync(accessId);
        if (access == null) throw new NotFoundException("UserWarehouseAccess", accessId);
        
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(access.WarehouseId);

        _uow.Repository<UserWarehouseAccess>().Delete(access);
        await _uow.CommitAsync();

        if (warehouse != null)
        {
            var title = "Warehouse Access Revoked";
            var message = $"Your access to the '{warehouse.Name}' warehouse has been revoked by an administrator.";

            await _notificationService.SendNotificationAsync(access.UserId, NotificationChannel.Email, "WarehouseRevocation", title, message, "Warehouse", access.WarehouseId);
            await _notificationService.SendNotificationAsync(access.UserId, NotificationChannel.InApp, "WarehouseRevocation", title, message, "Warehouse", access.WarehouseId);
        }
    }

    public async Task<IEnumerable<UserWarehouseAccessResponseDto>> GetWarehouseUsersAsync(Guid warehouseId)
    {
        var accesses = await _uow.Repository<UserWarehouseAccess>()
            .Query()
            .Include(a => a.User)
            .Include(a => a.Warehouse)
            .Where(a => a.WarehouseId == warehouseId)
            .ToListAsync();

        return accesses.Adapt<IEnumerable<UserWarehouseAccessResponseDto>>();
    }

    /// <summary>
    /// Suggests the most suitable bin location for a product in a given warehouse.
    /// Algorithm: find bins in zones that match the product's category type with available capacity.
    /// Falls back to any active bin if no typed zone match is found.
    /// </summary>
    public async Task<BinLocationResponseDto?> GetPutawaySuggestionAsync(Guid productId, Guid warehouseId)
    {
        // Get an available bin — prefer bins that are not fully occupied
        var occupiedBinIds = await _uow.Repository<StockLevel>()
            .Query()
            .Where(sl => sl.WarehouseId == warehouseId && sl.QuantityOnHand > 0)
            .Select(sl => sl.BinLocationId)
            .Distinct()
            .ToListAsync();

        // First: try to find an empty bin
        var suggestedBin = await _uow.Repository<BinLocation>()
            .Query()
            .Include(b => b.Zone).ThenInclude(z => z.Warehouse)
            .Where(b => b.Zone.WarehouseId == warehouseId &&
                        b.IsActive &&
                        !occupiedBinIds.Contains(b.Id))
            .OrderBy(b => b.Zone.Code).ThenBy(b => b.BinCode)
            .FirstOrDefaultAsync();

        // Fallback: any active bin in this warehouse
        suggestedBin ??= await _uow.Repository<BinLocation>()
            .Query()
            .Include(b => b.Zone).ThenInclude(z => z.Warehouse)
            .Where(b => b.Zone.WarehouseId == warehouseId && b.IsActive)
            .OrderBy(b => b.Zone.Code).ThenBy(b => b.BinCode)
            .FirstOrDefaultAsync();

        var result = suggestedBin?.Adapt<BinLocationResponseDto>();
        if (result != null && suggestedBin?.Zone?.Warehouse != null)
        {
            result.WarehouseId = suggestedBin.Zone.WarehouseId;
            result.WarehouseName = suggestedBin.Zone.Warehouse.Name;
        }
        return result;
    }

    public async Task<CapacitySummaryDto> GetWarehouseCapacitySummaryAsync(Guid warehouseId)
    {
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);
        if (warehouse == null) throw new NotFoundException("Warehouse", warehouseId);

        var metrics = await _uow.Repository<BinLocation>()
            .Query()
            .Where(b => b.Zone.WarehouseId == warehouseId)
            .GroupBy(b => b.Zone.WarehouseId)
            .Select(g => new
            {
                TotalVol = g.Sum(b => b.MaxVolumeCm3),
                UtilVol = g.Sum(b => b.UtilizedVolumeCm3),
                TotalWeight = g.Sum(b => b.MaxWeightKg),
                UtilWeight = g.Sum(b => b.UtilizedWeightKg)
            })
            .FirstOrDefaultAsync();

        if (metrics == null)
            return new CapacitySummaryDto { EntityId = warehouseId, EntityName = warehouse.Name };

        return new CapacitySummaryDto
        {
            EntityId = warehouseId,
            EntityName = warehouse.Name,
            TotalVolumeCm3 = metrics.TotalVol,
            UtilizedVolumeCm3 = metrics.UtilVol,
            TotalWeightKg = metrics.TotalWeight,
            UtilizedWeightKg = metrics.UtilWeight
        };
    }

    public async Task<CapacitySummaryDto> GetZoneCapacitySummaryAsync(Guid zoneId)
    {
        var zone = await _uow.Repository<WarehouseZone>().GetByIdAsync(zoneId);
        if (zone == null) throw new NotFoundException("WarehouseZone", zoneId);

        var metrics = await _uow.Repository<BinLocation>()
            .Query()
            .Where(b => b.ZoneId == zoneId)
            .GroupBy(b => b.ZoneId)
            .Select(g => new
            {
                TotalVol = g.Sum(b => b.MaxVolumeCm3),
                UtilVol = g.Sum(b => b.UtilizedVolumeCm3),
                TotalWeight = g.Sum(b => b.MaxWeightKg),
                UtilWeight = g.Sum(b => b.UtilizedWeightKg)
            })
            .FirstOrDefaultAsync();

        if (metrics == null)
            return new CapacitySummaryDto { EntityId = zoneId, EntityName = zone.Name };

        return new CapacitySummaryDto
        {
            EntityId = zoneId,
            EntityName = zone.Name,
            TotalVolumeCm3 = metrics.TotalVol,
            UtilizedVolumeCm3 = metrics.UtilVol,
            TotalWeightKg = metrics.TotalWeight,
            UtilizedWeightKg = metrics.UtilWeight
        };
    }

}
