using StokTakip.Application;

namespace StokTakip.Infrastructure;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>();
    public async Task<T?> FindAsync(object id, CancellationToken ct = default) => await db.Set<T>().FindAsync([id], ct);
    public async Task AddAsync(T entity, CancellationToken ct = default) => await db.Set<T>().AddAsync(entity, ct);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
}

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repos = new();

    public IRepository<T> Repo<T>() where T : class
    {
        if (!_repos.TryGetValue(typeof(T), out var repo))
            _repos[typeof(T)] = repo = new Repository<T>(db);
        return (IRepository<T>)repo;
    }

    public Task<int> SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
