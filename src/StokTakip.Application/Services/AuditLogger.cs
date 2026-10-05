using System.Text.Json;
using System.Text.Json.Serialization;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class AuditLogger(IUnitOfWork uow, ICurrentUser me) : IAuditLogger
{
    static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public Task LogAsync(string action, string? entityName = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, int? userId = null, CancellationToken ct = default) =>
        uow.Repo<AuditLog>().AddAsync(new AuditLog
        {
            UserId = userId ?? me.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            IpAddress = me.IpAddress,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json),
            CreatedAt = DateTime.UtcNow
        }, ct);
}
