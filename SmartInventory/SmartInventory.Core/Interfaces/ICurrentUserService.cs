namespace SmartInventory.Core.Interfaces;
public interface ICurrentUserService
{
    Guid UserId { get; }

    string? Role { get; }

    Guid? WarehouseId { get; }

    string? IpAddress { get; }

    bool IsInRole(params string[] roles);

    System.Security.Claims.ClaimsPrincipal? Principal { get; }
}
