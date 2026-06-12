using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;


public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Notification>> GetPagedNotificationsAsync(NotificationQueryParameters queryParams, Guid userId)
    {
        var query = _dbSet
            .Where(n => n.UserId == userId)
            .AsQueryable();

        // Filter by Read/Unread state
        if (queryParams.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == queryParams.IsRead.Value);
        }

        int totalCount = await query.CountAsync();

        
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

    
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<Notification>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        // Mark all unread records for target user as read directly inside SQL context
        var unread = await _dbSet
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }
    }
}
