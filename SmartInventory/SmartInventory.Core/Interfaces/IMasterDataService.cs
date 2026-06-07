using System.Threading.Tasks;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IMasterDataService
{
    /// <summary>
    /// Retrieves all highly static master data (Categories, Warehouses, Roles) using a cached backend strategy.
    /// This prevents database saturation during application startup or dropdown rendering.
    /// </summary>
    Task<MasterDataResponseDto> GetMasterDataAsync();
}
