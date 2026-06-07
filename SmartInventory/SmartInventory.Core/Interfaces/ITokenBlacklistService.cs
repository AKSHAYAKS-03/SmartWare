using System;
using System.Threading.Tasks;

namespace SmartInventory.Core.Interfaces;

public interface ITokenBlacklistService
{
    Task BlacklistUserAsync(Guid userId);
    Task<bool> IsUserBlacklistedAsync(Guid userId, DateTime tokenIssuedAt);
}
