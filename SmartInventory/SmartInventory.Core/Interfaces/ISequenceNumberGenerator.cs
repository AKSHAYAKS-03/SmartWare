using System.Threading.Tasks;

namespace SmartInventory.Core.Interfaces;

public interface ISequenceNumberGenerator
{
    Task<string> GenerateAsync(string sequenceName, string prefix);
}
