using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class AuditQueryService(IUnitOfWork uow)
{
    public async Task<PagedResult<AuditLogDto>> QueryAsync(string? action, int? userId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(page, 1); size = Math.Clamp(size, 1, 200);
        var q = uow.Repo<AuditLog>().Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (userId is int u) q = q.Where(a => a.UserId == u);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(a => new AuditLogDto(a.Id, a.UserId, a.Action, a.EntityName, a.EntityId, a.IpAddress, a.OldValues, a.NewValues, a.CreatedAt)).ToList(), total, page, size);
    }
}
