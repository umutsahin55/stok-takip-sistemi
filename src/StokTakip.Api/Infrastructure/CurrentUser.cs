using System.Security.Claims;
using StokTakip.Application;
using StokTakip.Domain;

namespace StokTakip.Api.Infrastructure;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public int? UserId => int.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;
    public Role? UserRole => Enum.TryParse<Role>(Principal?.FindFirstValue("role"), out var r) ? r : null;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>[Authorize(Roles = ...)] için rol grupları. Yetki kontrolü endpoint (backend) seviyesindedir.</summary>
public static class Roles
{
    public const string Admins = "SuperAdmin,ItAdmin";
    public const string StockRead = "SuperAdmin,ItAdmin,WarehouseManager,UnitManager";
    public const string StockWrite = "SuperAdmin,WarehouseManager";
    public const string SuperAdmin = "SuperAdmin";
}
