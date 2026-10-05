using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class UserService(IUnitOfWork uow, IPasswordHasher hasher, IAuditLogger audit, ICurrentUser me)
{
    static UserDto Map(User u) => new(u.Id, u.Username, u.FullName, u.Role, u.ManagerId, u.IsActive);

    // IT Admin, Süper Admin kullanıcılarına dokunamaz ve Süper Admin rolü atayamaz (yetki yükseltme koruması).
    void EnsureCanAssign(Role target)
    {
        if (target == Role.SuperAdmin && me.UserRole != Role.SuperAdmin)
            throw AppException.Forbidden("Süper Admin rolünü yalnızca Süper Admin atayabilir.");
    }

    public async Task<List<UserDto>> ListAsync(CancellationToken ct) =>
        (await uow.Repo<User>().Query().AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct)).Select(Map).ToList();

    public async Task<UserDto> CreateAsync(CreateUserDto d, CancellationToken ct)
    {
        EnsureCanAssign(d.Role);
        var username = d.Username.Trim().ToLowerInvariant();
        var users = uow.Repo<User>();
        if (await users.Query().AnyAsync(u => u.Username == username, ct)) throw AppException.Conflict("Bu kullanıcı adı zaten var.");
        if (d.ManagerId is int mid && !await users.Query().AnyAsync(u => u.Id == mid, ct)) throw AppException.BadRequest("Yönetici bulunamadı.");

        var u = new User { Username = username, FullName = d.FullName.Trim(), PasswordHash = hasher.Hash(d.Password), Role = d.Role, ManagerId = d.ManagerId };
        await users.AddAsync(u, ct);
        await audit.LogAsync("UserCreated", "User", null, newValues: new { u.Username, u.Role, u.ManagerId }, ct: ct);
        await uow.SaveAsync(ct);
        return Map(u);
    }

    async Task<User> GetManageableAsync(int id, CancellationToken ct)
    {
        var u = await uow.Repo<User>().FindAsync(id, ct) ?? throw AppException.NotFound("Kullanıcı bulunamadı.");
        if (u.Role == Role.SuperAdmin && me.UserRole != Role.SuperAdmin) throw AppException.Forbidden("Bu kullanıcı üzerinde işlem yetkiniz yok.");
        return u;
    }

    public async Task<UserDto> ChangeRoleAsync(int id, ChangeRoleDto d, CancellationToken ct)
    {
        EnsureCanAssign(d.Role);
        var u = await GetManageableAsync(id, ct);
        var old = u.Role;
        u.Role = d.Role;
        u.TokenVersion++; // eski rolle verilmiş token'lar geçersiz
        await audit.LogAsync("RoleChanged", "User", id.ToString(), new { Role = old }, new { Role = d.Role }, ct: ct);
        await uow.SaveAsync(ct);
        return Map(u);
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordDto d, CancellationToken ct)
    {
        var u = await GetManageableAsync(id, ct);
        u.PasswordHash = hasher.Hash(d.NewPassword);
        u.TokenVersion++;
        await audit.LogAsync("PasswordReset", "User", id.ToString(), ct: ct); // parola/hash loglanmaz
        await uow.SaveAsync(ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var u = await GetManageableAsync(id, ct);
        if (id == me.UserId && !active) throw AppException.BadRequest("Kendi hesabınızı pasifleştiremezsiniz.");
        var old = u.IsActive;
        u.IsActive = active;
        if (!active) u.TokenVersion++;
        await audit.LogAsync("UserActiveChanged", "User", id.ToString(), new { IsActive = old }, new { IsActive = active }, ct: ct);
        await uow.SaveAsync(ct);
    }
}
