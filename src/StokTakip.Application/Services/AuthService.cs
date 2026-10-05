using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class AuthService(IUnitOfWork uow, IPasswordHasher hasher, ITokenService tokens, IAuditLogger audit, ICurrentUser me)
{
    static string? _dummyHash; // kullanıcı yokken de aynı sürede hash doğrulaması yapılır (timing/enumeration)

    public async Task<LoginResultDto> LoginAsync(LoginDto d, CancellationToken ct)
    {
        var username = d.Username.Trim().ToLowerInvariant();
        var user = await uow.Repo<User>().Query().FirstOrDefaultAsync(u => u.Username == username, ct);
        _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString());

        var passwordOk = hasher.Verify(d.Password, user?.PasswordHash ?? _dummyHash);
        if (user is null || !user.IsActive || !passwordOk)
        {
            await audit.LogAsync("LoginFailed", "User", user?.Id.ToString(), newValues: new { Username = username }, userId: user?.Id, ct: ct);
            await uow.SaveAsync(ct);
            throw AppException.Unauthorized("Kullanıcı adı veya parola hatalı.");
        }

        var (token, exp) = tokens.CreateToken(user);
        await audit.LogAsync("LoginSuccess", "User", user.Id.ToString(), userId: user.Id, ct: ct);
        await uow.SaveAsync(ct);
        return new LoginResultDto(token, exp, user.Id, user.Username, user.FullName, user.Role);
    }

    /// <summary>TokenVersion artırılır: eldeki JWT bir sonraki istekte anında reddedilir.</summary>
    public async Task LogoutAsync(CancellationToken ct)
    {
        var id = me.UserId ?? throw AppException.Unauthorized("Oturum bulunamadı.");
        var user = await uow.Repo<User>().FindAsync(id, ct) ?? throw AppException.Unauthorized("Oturum bulunamadı.");
        user.TokenVersion++;
        await audit.LogAsync("Logout", "User", id.ToString(), ct: ct);
        await uow.SaveAsync(ct);
    }
}
