using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class DashboardService(IUnitOfWork uow, StockService stock)
{
    /// <summary>Varsayım: stok &lt;= min → "Kritik Stok"; stok &lt;= min×1.5 → "Stok Azaldı".</summary>
    public static string StockLevel(int stock, int min) => stock <= min ? "Kritik Stok" : "Stok Azaldı";

    /// <summary>Eşikler: 90 / 60 / 30 gün; geçmişse Expired.</summary>
    public static string ExpiryLevel(DateOnly? expiry, DateOnly today)
    {
        if (expiry is null) return "Normal";
        var days = expiry.Value.DayNumber - today.DayNumber;
        return days < 0 ? "Expired" : days <= 30 ? "D30" : days <= 60 ? "D60" : days <= 90 ? "D90" : "Normal";
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var parts = uow.Repo<Part>().Query().AsNoTracking().Where(p => p.IsActive);
        var totalProducts = await parts.CountAsync(ct);
        var totalStock = await parts.SumAsync(p => (int?)p.CurrentStock, ct) ?? 0;
        var critical = await parts.CountAsync(p => p.CurrentStock <= p.MinStock, ct);
        var low = await parts.CountAsync(p => p.CurrentStock > p.MinStock && p.CurrentStock * 2 <= p.MinStock * 3, ct);

        var criticalParts = (await parts.Where(p => p.CurrentStock * 2 <= p.MinStock * 3)
                .OrderBy(p => p.CurrentStock - p.MinStock).Take(20).ToListAsync(ct))
            .Select(p => new CriticalPartDto(p.Id, p.Code, p.Name, p.CurrentStock, p.MinStock, StockLevel(p.CurrentStock, p.MinStock))).ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = today.AddDays(90);
        var expiring = (await uow.Repo<StockLot>().Query().AsNoTracking()
                .Where(l => l.Quantity > 0 && l.ExpiryDate != null && l.ExpiryDate <= limit)
                .OrderBy(l => l.ExpiryDate).Take(50)
                .Select(l => new { l.Part.Code, l.Part.Name, Warehouse = l.Warehouse.Name, l.LotNumber, l.ExpiryDate, l.Quantity })
                .ToListAsync(ct))
            .Select(l => new ExpiringLotDto(l.Code, l.Name, l.Warehouse, l.LotNumber, l.ExpiryDate!.Value,
                l.ExpiryDate.Value.DayNumber - today.DayNumber, ExpiryLevel(l.ExpiryDate, today), l.Quantity)).ToList();

        var byWarehouse = await uow.Repo<StockLot>().Query().AsNoTracking()
            .GroupBy(l => l.Warehouse.Name).Select(g => new WarehouseStockDto(g.Key, g.Sum(x => x.Quantity))).ToListAsync(ct);

        var recent = (await stock.GetMovementsAsync(null, 1, 10, ct)).Items;
        return new DashboardDto(totalProducts, totalStock, critical, low, criticalParts, expiring, byWarehouse, recent);
    }
}
