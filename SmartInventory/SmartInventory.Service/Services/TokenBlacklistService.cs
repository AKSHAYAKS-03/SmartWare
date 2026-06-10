using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _memoryCache;

    public TokenBlacklistService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task BlacklistUserAsync(Guid userId)
    {
        var cacheKey = $"Blacklist_{userId}";
        // Store the time of blacklist. Any token issued before this time is invalid.
        // Cache it for max access token lifetime (e.g. 1 day to be safe).
        _memoryCache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromDays(1));
        return Task.CompletedTask;
    }

    public Task<bool> IsUserBlacklistedAsync(Guid userId, DateTime tokenIssuedAt)
    {
        var cacheKey = $"Blacklist_{userId}";
        if (_memoryCache.TryGetValue(cacheKey, out DateTime blacklistTime))
        {
            if (tokenIssuedAt <= blacklistTime)
            {
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
}
