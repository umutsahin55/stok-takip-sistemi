using StokTakip.Domain;

namespace StokTakip.Application;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<T?> FindAsync(object id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<T> Repo<T>() where T : class;
    /// <summary>Tüm bekleyen değişiklikleri tek transaction'da kaydeder.</summary>
    Task<int> SaveAsync(CancellationToken ct = default);
}

public interface ICurrentUser
{
    int? UserId { get; }
    Role? UserRole { get; }
    string? IpAddress { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public interface IAuditLogger
{
    /// <summary>Kaydı unit of work'e ekler; kaydetme işini çağıran yapar (iş ile aynı transaction).</summary>
    Task LogAsync(string action, string? entityName = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, int? userId = null, CancellationToken ct = default);
}

public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static AppException BadRequest(string m) => new(400, m);
    public static AppException Unauthorized(string m) => new(401, m);
    public static AppException Forbidden(string m) => new(403, m);
    public static AppException NotFound(string m) => new(404, m);
    public static AppException Conflict(string m) => new(409, m);
}
