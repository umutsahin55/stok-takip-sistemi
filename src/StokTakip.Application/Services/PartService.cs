using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class PartService(IUnitOfWork uow, CategoryService categories, IAuditLogger audit)
{
    static PartDto Map(Part p, string path) => new(p.Id, p.Code, p.Barcode, p.Name, p.CategoryId, path, p.Unit,
        p.PurchasePrice, p.SalePrice, p.MinStock, p.CurrentStock, p.IsActive, p.TracksLots, p.IssuePolicy);

    public async Task<PagedResult<PartDto>> SearchAsync(string? q, int? categoryId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(page, 1); size = Math.Clamp(size, 1, 100);
        var query = uow.Repo<Part>().Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p => p.Code.Contains(q) || p.Name.Contains(q) || p.Barcode.Contains(q)); // parametreli LIKE
        }
        if (categoryId is int cid)
        {
            var ids = await categories.GetSubtreeIdsAsync(cid, ct);
            query = query.Where(p => ids.Contains(p.CategoryId));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Code).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var paths = await categories.GetPathsAsync(ct);
        return new(items.Select(p => Map(p, paths.GetValueOrDefault(p.CategoryId, ""))).ToList(), total, page, size);
    }

    public async Task<PartDto> GetAsync(int id, CancellationToken ct)
    {
        var p = await uow.Repo<Part>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Parça bulunamadı.");
        return Map(p, (await categories.GetPathsAsync(ct)).GetValueOrDefault(p.CategoryId, ""));
    }

    public async Task<PartDto> CreateAsync(PartUpsertDto d, CancellationToken ct)
    {
        var repo = uow.Repo<Part>();
        var code = d.Code.Trim().ToUpperInvariant();
        var barcode = d.Barcode.Trim();
        if (await repo.Query().AnyAsync(p => p.Code == code || p.Barcode == barcode, ct))
            throw AppException.Conflict("Bu parça kodu veya barkod zaten kayıtlı.");
        if (!await uow.Repo<Category>().Query().AnyAsync(c => c.Id == d.CategoryId, ct)) throw AppException.BadRequest("Kategori bulunamadı.");

        var p = new Part
        {
            Code = code, Barcode = barcode, Name = d.Name.Trim(), CategoryId = d.CategoryId, Unit = d.Unit.Trim(),
            PurchasePrice = d.PurchasePrice, SalePrice = d.SalePrice, MinStock = d.MinStock, IsActive = d.IsActive,
            TracksLots = d.TracksLots, IssuePolicy = d.IssuePolicy
        };
        await repo.AddAsync(p, ct);
        await audit.LogAsync("PartCreated", "Part", null, newValues: new { code, barcode, p.Name, p.CategoryId }, ct: ct);
        await uow.SaveAsync(ct); // DB unique index yarış durumlarına karşı son savunma (middleware 409'a çevirir)
        return await GetAsync(p.Id, ct);
    }

    /// <summary>Stok alanları (CurrentStock) güncellenemez; kod değiştirilemez.</summary>
    public async Task<PartDto> UpdateAsync(int id, PartUpsertDto d, CancellationToken ct)
    {
        var repo = uow.Repo<Part>();
        var p = await repo.FindAsync(id, ct) ?? throw AppException.NotFound("Parça bulunamadı.");
        if (!string.Equals(p.Code, d.Code.Trim(), StringComparison.OrdinalIgnoreCase)) throw AppException.BadRequest("Parça kodu değiştirilemez.");
        var barcode = d.Barcode.Trim();
        if (await repo.Query().AnyAsync(x => x.Id != id && x.Barcode == barcode, ct)) throw AppException.Conflict("Bu barkod başka bir parçada kayıtlı.");
        if (!await uow.Repo<Category>().Query().AnyAsync(c => c.Id == d.CategoryId, ct)) throw AppException.BadRequest("Kategori bulunamadı.");

        var old = new { p.Name, p.Barcode, p.CategoryId, p.PurchasePrice, p.SalePrice, p.MinStock, p.IsActive, p.IssuePolicy };
        p.Name = d.Name.Trim(); p.Barcode = barcode; p.CategoryId = d.CategoryId; p.Unit = d.Unit.Trim();
        p.PurchasePrice = d.PurchasePrice; p.SalePrice = d.SalePrice; p.MinStock = d.MinStock;
        p.IsActive = d.IsActive; p.TracksLots = d.TracksLots; p.IssuePolicy = d.IssuePolicy;
        await audit.LogAsync("PartUpdated", "Part", id.ToString(), old,
            new { p.Name, p.Barcode, p.CategoryId, p.PurchasePrice, p.SalePrice, p.MinStock, p.IsActive, p.IssuePolicy }, ct: ct);
        await uow.SaveAsync(ct);
        return await GetAsync(id, ct);
    }
}
