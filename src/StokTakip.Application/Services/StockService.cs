using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class StockService(IUnitOfWork uow, ICurrentUser me, IAuditLogger audit)
{
    const string DefaultLot = "DEFAULT";
    static bool IsIncoming(MovementType t) => t is MovementType.GoodsReceipt or MovementType.Return;

    /// <summary>
    /// Tek giriş noktası: stok adedi yalnızca buradan değişir.
    /// Çıkışlarda FEFO/FIFO otomatik uygulanır; süresi dolmuş partiler (hasarlı düşüş hariç) çıkışa alınmaz.
    /// Parça + parti + hareket + audit kaydı tek SaveChanges (tek transaction) ile yazılır;
    /// RowVersion eşzamanlılık belirteci çifte harcamayı engeller (409).
    /// </summary>
    public async Task<StockResultDto> MoveAsync(MoveStockDto d, CancellationToken ct)
    {
        var userId = me.UserId ?? throw AppException.Unauthorized("Oturum bulunamadı.");
        var part = await uow.Repo<Part>().FindAsync(d.PartId, ct) ?? throw AppException.NotFound("Parça bulunamadı.");
        if (!part.IsActive) throw AppException.BadRequest("Pasif parça için stok hareketi yapılamaz.");
        var wh = await uow.Repo<Warehouse>().FindAsync(d.WarehouseId, ct);
        if (wh is null || !wh.IsActive) throw AppException.NotFound("Depo bulunamadı.");

        var lots = uow.Repo<StockLot>();
        var movements = uow.Repo<StockMovement>();
        var lines = new List<MovementLineDto>();
        var now = DateTime.UtcNow;

        if (IsIncoming(d.Type))
        {
            var lotNo = part.TracksLots ? d.LotNumber?.Trim() : DefaultLot;
            if (string.IsNullOrEmpty(lotNo)) throw AppException.BadRequest("Parti takipli parça için lot numarası zorunludur.");

            var lot = await lots.Query().FirstOrDefaultAsync(l => l.PartId == part.Id && l.WarehouseId == wh.Id && l.LotNumber == lotNo, ct);
            if (lot is null)
            {
                lot = new StockLot { PartId = part.Id, WarehouseId = wh.Id, LotNumber = lotNo, ProductionDate = d.ProductionDate, ExpiryDate = d.ExpiryDate, ReceivedAt = now };
                await lots.AddAsync(lot, ct);
            }
            lot.Apply(d.Quantity);
            await movements.AddAsync(new StockMovement { PartId = part.Id, WarehouseId = wh.Id, Lot = lot, Quantity = d.Quantity, Type = d.Type, Note = d.Note, UserId = userId, CreatedAt = now }, ct);
            lines.Add(new(lotNo, d.Quantity));
            part.ApplyDelta(d.Quantity);
        }
        else
        {
            var today = DateOnly.FromDateTime(now);
            var q = lots.Query().Where(l => l.PartId == part.Id && l.WarehouseId == wh.Id && l.Quantity > 0);
            if (d.Type != MovementType.Damaged) q = q.Where(l => l.ExpiryDate == null || l.ExpiryDate >= today);

            var ordered = part.IssuePolicy == IssuePolicy.FEFO
                ? q.OrderBy(l => l.ExpiryDate == null).ThenBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)   // SKT'si yaklaşan önce, SKT'siz en sona
                : q.OrderBy(l => l.ReceivedAt);                                                              // FIFO
            var candidates = await ordered.ToListAsync(ct);

            var available = candidates.Sum(l => l.Quantity);
            if (available < d.Quantity) throw AppException.Conflict($"Yetersiz stok. Kullanılabilir: {available}, istenen: {d.Quantity}.");

            var remaining = d.Quantity;
            foreach (var lot in candidates)
            {
                if (remaining == 0) break;
                var take = Math.Min(lot.Quantity, remaining);
                lot.Apply(-take);
                remaining -= take;
                await movements.AddAsync(new StockMovement { PartId = part.Id, WarehouseId = wh.Id, Lot = lot, Quantity = -take, Type = d.Type, Note = d.Note, UserId = userId, CreatedAt = now }, ct);
                lines.Add(new(lot.LotNumber, -take));
            }
            part.ApplyDelta(-d.Quantity);
        }

        await audit.LogAsync("StockMovement", "Part", part.Id.ToString(), null,
            new { d.Type, WarehouseId = wh.Id, d.Quantity, Lots = lines, part.CurrentStock }, ct: ct);
        await uow.SaveAsync(ct);
        return new StockResultDto(part.Id, part.CurrentStock, lines);
    }

    public async Task<PagedResult<MovementDto>> GetMovementsAsync(int? partId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(page, 1); size = Math.Clamp(size, 1, 100);
        var q = uow.Repo<StockMovement>().Query().AsNoTracking();
        if (partId is int pid) q = q.Where(m => m.PartId == pid);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(m => m.Id).Skip((page - 1) * size).Take(size)
            .Select(m => new { m.Id, m.PartId, PartCode = m.Part.Code, Warehouse = m.Warehouse.Name, Lot = m.Lot != null ? m.Lot.LotNumber : null, m.Quantity, m.Type, m.Note, User = m.User.FullName, m.CreatedAt })
            .ToListAsync(ct);
        return new(rows.Select(r => new MovementDto(r.Id, r.PartId, r.PartCode, r.Warehouse, r.Lot, r.Quantity, r.Type, r.Note, r.User, r.CreatedAt)).ToList(), total, page, size);
    }

    public async Task<PartLotsDto> GetLotsAsync(int partId, CancellationToken ct)
    {
        var part = await uow.Repo<Part>().Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == partId, ct) ?? throw AppException.NotFound("Parça bulunamadı.");
        var rows = await uow.Repo<StockLot>().Query().AsNoTracking().Where(l => l.PartId == partId && l.Quantity > 0)
            .OrderBy(l => l.ExpiryDate == null).ThenBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)
            .Select(l => new { l.Id, Warehouse = l.Warehouse.Name, l.LotNumber, l.ProductionDate, l.ExpiryDate, l.Quantity })
            .ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lots = rows.Select(r => new LotDto(r.Id, r.Warehouse, r.LotNumber, r.ProductionDate, r.ExpiryDate, r.Quantity, DashboardService.ExpiryLevel(r.ExpiryDate, today))).ToList();
        return new(part.Id, part.Code, part.Name, part.IssuePolicy, lots.Sum(l => l.Quantity), lots);
    }

    public async Task<List<WarehouseDto>> GetWarehousesAsync(CancellationToken ct) =>
        await uow.Repo<Warehouse>().Query().AsNoTracking().Where(w => w.IsActive).OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.Type)).ToListAsync(ct);
}
