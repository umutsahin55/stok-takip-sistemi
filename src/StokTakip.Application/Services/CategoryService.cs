using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Application.Services;

public class CategoryService(IUnitOfWork uow, IAuditLogger audit)
{
    public async Task<List<CategoryNodeDto>> GetTreeAsync(CancellationToken ct)
    {
        var all = await uow.Repo<Category>().Query().AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var nodes = all.ToDictionary(c => c.Id, c => new CategoryNodeDto(c.Id, c.Name, c.ParentId, new()));
        var roots = new List<CategoryNodeDto>();
        foreach (var c in all)
        {
            if (c.ParentId is int p && nodes.TryGetValue(p, out var parent)) parent.Children.Add(nodes[c.Id]);
            else roots.Add(nodes[c.Id]);
        }
        return roots;
    }

    /// <summary>Id → "Üst > Alt > Kategori" yolu.</summary>
    public async Task<Dictionary<int, string>> GetPathsAsync(CancellationToken ct)
    {
        var all = await uow.Repo<Category>().Query().AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var paths = new Dictionary<int, string>();
        foreach (var c in all.Values)
        {
            var parts = new List<string>();
            for (Category? cur = c; cur is not null; cur = cur.ParentId is int p ? all.GetValueOrDefault(p) : null) parts.Add(cur.Name);
            parts.Reverse();
            paths[c.Id] = string.Join(" > ", parts);
        }
        return paths;
    }

    /// <summary>Kategori + tüm alt kategorilerinin id'leri.</summary>
    public async Task<HashSet<int>> GetSubtreeIdsAsync(int id, CancellationToken ct)
    {
        var edges = await uow.Repo<Category>().Query().AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
        var byParent = edges.Where(e => e.ParentId != null).GroupBy(e => e.ParentId!.Value).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var result = new HashSet<int> { id };
        var stack = new Stack<int>(new[] { id });
        while (stack.Count > 0)
            if (byParent.TryGetValue(stack.Pop(), out var kids))
                foreach (var k in kids) if (result.Add(k)) stack.Push(k);
        return result;
    }

    public async Task<CategoryNodeDto> CreateAsync(CategoryCreateDto d, CancellationToken ct)
    {
        var repo = uow.Repo<Category>();
        var name = d.Name.Trim();
        if (d.ParentId is int pid && !await repo.Query().AnyAsync(c => c.Id == pid, ct)) throw AppException.NotFound("Üst kategori bulunamadı.");
        if (await repo.Query().AnyAsync(c => c.ParentId == d.ParentId && c.Name == name, ct)) throw AppException.Conflict("Aynı seviyede aynı isimde kategori var.");

        var c = new Category { Name = name, ParentId = d.ParentId };
        await repo.AddAsync(c, ct);
        await audit.LogAsync("CategoryCreated", "Category", null, newValues: new { name, d.ParentId }, ct: ct);
        await uow.SaveAsync(ct);
        return new CategoryNodeDto(c.Id, c.Name, c.ParentId, new());
    }

    public async Task MoveAsync(int id, int? newParentId, CancellationToken ct)
    {
        var repo = uow.Repo<Category>();
        var c = await repo.FindAsync(id, ct) ?? throw AppException.NotFound("Kategori bulunamadı.");
        if (newParentId is not null)
        {
            // Döngü koruması: yeni üst, kendisinin alt ağacında olamaz.
            if ((await GetSubtreeIdsAsync(id, ct)).Contains(newParentId.Value))
                throw AppException.BadRequest("Kategori kendi alt kategorisinin altına taşınamaz.");
            if (!await repo.Query().AnyAsync(x => x.Id == newParentId, ct)) throw AppException.NotFound("Yeni üst kategori bulunamadı.");
        }
        var old = c.ParentId;
        c.ParentId = newParentId;
        await audit.LogAsync("CategoryMoved", "Category", id.ToString(), new { ParentId = old }, new { ParentId = newParentId }, ct: ct);
        await uow.SaveAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var repo = uow.Repo<Category>();
        var c = await repo.FindAsync(id, ct) ?? throw AppException.NotFound("Kategori bulunamadı.");
        if (await repo.Query().AnyAsync(x => x.ParentId == id, ct)) throw AppException.Conflict("Alt kategorisi olan kategori silinemez.");
        if (await uow.Repo<Part>().Query().AnyAsync(p => p.CategoryId == id, ct)) throw AppException.Conflict("İçinde parça bulunan kategori silinemez.");
        repo.Remove(c);
        await audit.LogAsync("CategoryDeleted", "Category", id.ToString(), new { c.Name, c.ParentId }, ct: ct);
        await uow.SaveAsync(ct);
    }
}
