using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokTakip.Api.Infrastructure;
using StokTakip.Application;
using StokTakip.Application.Services;

namespace StokTakip.Api.Controllers;

[ApiController, Route("api/users"), Authorize(Roles = Roles.Admins)]
public class UsersController(UserService svc) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.ListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(dto, ct));
    [HttpPut("{id:int}/role")] public async Task<IActionResult> Role(int id, ChangeRoleDto dto, CancellationToken ct) => Ok(await svc.ChangeRoleAsync(id, dto, ct));
    [HttpPost("{id:int}/reset-password")] public async Task<IActionResult> Reset(int id, ResetPasswordDto dto, CancellationToken ct) { await svc.ResetPasswordAsync(id, dto, ct); return NoContent(); }
    [HttpPut("{id:int}/active")] public async Task<IActionResult> Active(int id, SetActiveDto dto, CancellationToken ct) { await svc.SetActiveAsync(id, dto.IsActive, ct); return NoContent(); }
}

[ApiController, Route("api/audit-logs"), Authorize(Roles = Roles.Admins)]
public class AuditLogsController(AuditQueryService svc) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Query(string? action, int? userId, int page = 1, int size = 50, CancellationToken ct = default) => Ok(await svc.QueryAsync(action, userId, page, size, ct));
}

[ApiController, Route("api/tickets")] // tüm giriş yapmış kullanıcılar; kapsam servis katmanında (IDOR koruması)
public class TicketsController(SupportService svc) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.ListAsync(ct));
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));
    [HttpPost] public async Task<IActionResult> Create(TicketCreateDto dto, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(dto, ct));
    [HttpPut("{id:int}/status"), Authorize(Roles = Roles.Admins)]
    public async Task<IActionResult> Status(int id, TicketStatusDto dto, CancellationToken ct) => Ok(await svc.UpdateStatusAsync(id, dto, ct));
}

[ApiController, Route("api/leaves")]
public class LeavesController(LeaveService svc) : ControllerBase
{
    [HttpGet("mine")] public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await svc.MineAsync(ct));
    [HttpGet("pending")] public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await svc.PendingForMeAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(LeaveCreateDto dto, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(dto, ct));
    [HttpPost("{id:int}/decision")] public async Task<IActionResult> Decide(int id, LeaveDecisionDto dto, CancellationToken ct) => Ok(await svc.DecideAsync(id, dto.Approve, ct));
}
