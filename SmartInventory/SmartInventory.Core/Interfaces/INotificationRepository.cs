using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;
public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<PagedResult<Notification>> GetPagedNotificationsAsync(NotificationQueryParameters queryParams, Guid userId);

    Task MarkAllAsReadAsync(Guid userId);
}
