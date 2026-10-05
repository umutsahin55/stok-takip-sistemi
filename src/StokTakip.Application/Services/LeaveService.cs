using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

/// <summary>Personel → Bağlı Yönetici → İK. Varsayım: "İK" onayını SuperAdmin rolü verir (şartnamede ayrı İK rolü yok).</summary>
public class LeaveService(IUnitOfWork uow, ICurrentUser me, IAuditLogger audit)
{
    int Me => me.UserId ?? throw AppException.Unauthorized("Oturum bulunamadı.");
    static LeaveDto Map(LeaveRequest r) => new(r.Id, r.User.FullName, r.StartDate, r.EndDate, r.Reason, r.Status, r.CreatedAt);

    public async Task<LeaveDto> CreateAsync(LeaveCreateDto d, CancellationToken ct)
    {
        var user = await uow.Repo<User>().FindAsync(Me, ct) ?? throw AppException.Unauthorized("Oturum bulunamadı.");
        var overlap = await uow.Repo<LeaveRequest>().Query().AnyAsync(r => r.UserId == user.Id && r.Status != LeaveStatus.Rejected && r.StartDate <= d.EndDate && r.EndDate >= d.StartDate, ct);
        if (overlap) throw AppException.Conflict("Bu tarihlerle çakışan bir izin talebiniz var.");

        var r = new LeaveRequest
        {
            UserId = user.Id, StartDate = d.StartDate, EndDate = d.EndDate, Reason = d.Reason.Trim(), CreatedAt = DateTime.UtcNow,
            Status = user.ManagerId is null ? LeaveStatus.PendingHr : LeaveStatus.PendingManager // yöneticisi yoksa doğrudan İK
        };
        await uow.Repo<LeaveRequest>().AddAsync(r, ct);
        await audit.LogAsync("LeaveRequested", "LeaveRequest", null, newValues: new { d.StartDate, d.EndDate, r.Status }, ct: ct);
        await uow.SaveAsync(ct);
        r.User = user;
        return Map(r);
    }

    public async Task<List<LeaveDto>> MineAsync(CancellationToken ct) =>
        (await uow.Repo<LeaveRequest>().Query().AsNoTracking().Include(r => r.User).Where(r => r.UserId == Me).OrderByDescending(r => r.Id).ToListAsync(ct)).Select(Map).ToList();

    /// <summary>Benim onayımı bekleyenler: ekibimin yönetici aşamasındakiler (+ SuperAdmin için İK aşamasındakiler).</summary>
    public async Task<List<LeaveDto>> PendingForMeAsync(CancellationToken ct)
    {
        var meId = Me; var isHr = me.UserRole == Role.SuperAdmin;
        var rows = await uow.Repo<LeaveRequest>().Query().AsNoTracking().Include(r => r.User)
            .Where(r => r.UserId != meId &&
                ((r.Status == LeaveStatus.PendingManager && r.User.ManagerId == meId) || (isHr && r.Status == LeaveStatus.PendingHr)))
            .OrderBy(r => r.Id).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<LeaveDto> DecideAsync(int id, bool approve, CancellationToken ct)
    {
        var meId = Me;
        var r = await uow.Repo<LeaveRequest>().Query().Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("İzin talebi bulunamadı.");
        if (r.UserId == meId) throw AppException.Forbidden("Kendi izin talebinizi onaylayamazsınız.");

        var old = r.Status;
        switch (r.Status)
        {
            case LeaveStatus.PendingManager:
                if (r.User.ManagerId != meId) throw AppException.Forbidden("Bu talebi onaylama yetkiniz yok.");
                r.ManagerApprovedById = meId;
                r.Status = approve ? LeaveStatus.PendingHr : LeaveStatus.Rejected;
                break;
            case LeaveStatus.PendingHr:
                if (me.UserRole != Role.SuperAdmin) throw AppException.Forbidden("Bu aşamayı yalnızca İK yetkilisi onaylayabilir.");
                r.HrApprovedById = meId;
                r.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
                break;
            default:
                throw AppException.BadRequest("Talep zaten sonuçlanmış.");
        }
        await audit.LogAsync("LeaveDecision", "LeaveRequest", id.ToString(), new { Status = old }, new { r.Status }, ct: ct);
        await uow.SaveAsync(ct);
        return Map(r);
    }
}
