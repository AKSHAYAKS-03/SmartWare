using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for Notification entity operations.
/// </summary>
public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>
    /// Fetches a paginated set of notifications for a specific user.
    /// </summary>
    Task<PagedResult<Notification>> GetPagedNotificationsAsync(NotificationQueryParameters queryParams, Guid userId);

    /// <summary>
    /// Marks all unread notifications for a user as read.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);
}
