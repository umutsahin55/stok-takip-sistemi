using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class SupportService(IUnitOfWork uow, ICurrentUser me, IAuditLogger audit)
{
    bool IsAdmin => me.UserRole is Role.SuperAdmin or Role.ItAdmin;
    int Me => me.UserId ?? throw AppException.Unauthorized("Oturum bulunamadı.");

    static TicketDto Map(SupportTicket t) => new(t.Id, t.Title, t.Description, t.Category, t.Status,
        t.CreatedBy.FullName, t.AssignedTo?.FullName, t.CreatedAt, t.UpdatedAt);

    IQueryable<SupportTicket> Visible() =>
        uow.Repo<SupportTicket>().Query().Include(t => t.CreatedBy).Include(t => t.AssignedTo)
           .Where(t => IsAdmin || t.CreatedById == Me);   // IDOR: admin değilse yalnızca kendi talepleri

    public async Task<TicketDto> CreateAsync(TicketCreateDto d, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var t = new SupportTicket { Title = d.Title.Trim(), Description = d.Description.Trim(), Category = d.Category, CreatedById = Me, CreatedAt = now, UpdatedAt = now };
        await uow.Repo<SupportTicket>().AddAsync(t, ct);
        await uow.SaveAsync(ct);
        return await GetAsync(t.Id, ct);
    }

    public async Task<List<TicketDto>> ListAsync(CancellationToken ct) =>
        (await Visible().AsNoTracking().OrderByDescending(t => t.Id).Take(200).ToListAsync(ct)).Select(Map).ToList();

    public async Task<TicketDto> GetAsync(int id, CancellationToken ct) =>
        Map(await Visible().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw AppException.NotFound("Talep bulunamadı.")); // başkasınınki de 404

    public async Task<TicketDto> UpdateStatusAsync(int id, TicketStatusDto d, CancellationToken ct)
    {
        if (!IsAdmin) throw AppException.Forbidden("Talep durumunu yalnızca IT yetkilileri değiştirebilir.");
        var t = await uow.Repo<SupportTicket>().FindAsync(id, ct) ?? throw AppException.NotFound("Talep bulunamadı.");
        if (d.AssignedToId is int a && !await uow.Repo<User>().Query().AnyAsync(u => u.Id == a && u.IsActive, ct)) throw AppException.BadRequest("Atanacak kullanıcı bulunamadı.");
        var old = new { t.Status, t.AssignedToId };
        t.Status = d.Status; t.AssignedToId = d.AssignedToId; t.UpdatedAt = DateTime.UtcNow;
        await audit.LogAsync("TicketUpdated", "SupportTicket", id.ToString(), old, new { d.Status, d.AssignedToId }, ct: ct);
        await uow.SaveAsync(ct);
        return await GetAsync(id, ct);
    }
}
