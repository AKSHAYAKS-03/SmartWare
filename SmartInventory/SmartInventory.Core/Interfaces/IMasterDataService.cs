using System.Threading.Tasks;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IMasterDataService
{

    Task<MasterDataResponseDto> GetMasterDataAsync();
}
