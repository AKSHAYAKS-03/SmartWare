using SmartInventory.Core.DTOs;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Interfaces;
public interface IWarehouseService
{
    Task<WarehouseResponseDto> CreateWarehouseAsync(WarehouseCreateDto dto);
    Task<WarehouseResponseDto> UpdateWarehouseAsync(Guid warehouseId, WarehouseUpdateDto dto);
    Task DeleteWarehouseAsync(Guid warehouseId);
    Task<WarehouseResponseDto> GetWarehouseByIdAsync(Guid warehouseId);
    Task<PagedResult<WarehouseResponseDto>> GetWarehousesAsync(QueryParameters queryParams);

    Task<ZoneResponseDto> CreateZoneAsync(ZoneCreateDto dto);
    Task<ZoneResponseDto> UpdateZoneAsync(Guid zoneId, ZoneUpdateDto dto);
    Task DeleteZoneAsync(Guid zoneId);
    Task<IEnumerable<ZoneResponseDto>> GetZonesByWarehouseAsync(Guid warehouseId);

    Task<BinLocationResponseDto> CreateBinLocationAsync(BinLocationCreateDto dto);
    Task<BinLocationResponseDto> UpdateBinLocationAsync(Guid binId, BinLocationUpdateDto dto);
    Task DeleteBinLocationAsync(Guid binId);
    Task<IEnumerable<BinLocationResponseDto>> GetBinsByZoneAsync(Guid zoneId);

    Task<UserWarehouseAccessResponseDto> AssignUserAccessAsync(UserWarehouseAccessCreateDto dto);
    Task RevokeUserAccessAsync(Guid accessId);
    Task<IEnumerable<UserWarehouseAccessResponseDto>> GetWarehouseUsersAsync(Guid warehouseId);

    Task<BinLocationResponseDto?> GetPutawaySuggestionAsync(Guid productId, Guid warehouseId);

    Task<CapacitySummaryDto> GetWarehouseCapacitySummaryAsync(Guid warehouseId);
    Task<CapacitySummaryDto> GetZoneCapacitySummaryAsync(Guid zoneId);
}
