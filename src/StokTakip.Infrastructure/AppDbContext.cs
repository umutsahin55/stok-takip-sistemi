using Microsoft.EntityFrameworkCore;
using StokTakip.Domain;

namespace StokTakip.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId);
        });

        b.Entity<Warehouse>(e => { e.ToTable("Warehouses"); e.Property(x => x.Name).HasMaxLength(100).IsRequired(); });

        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
            e.HasIndex(x => x.ParentId);
        });

        b.Entity<Part>(e =>
        {
            e.ToTable("Parts");
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            e.Property(x => x.PurchasePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.SalePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Code).IsUnique();      // mükerrer kod DB seviyesinde engellenir
            e.HasIndex(x => x.Barcode).IsUnique();
            e.HasIndex(x => x.CategoryId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<StockLot>(e =>
        {
            e.ToTable("StockLots");
            e.Property(x => x.LotNumber).HasMaxLength(50).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.PartId, x.WarehouseId, x.LotNumber }).IsUnique();
            e.HasIndex(x => new { x.PartId, x.WarehouseId, x.ExpiryDate });
        });

        b.Entity<StockMovement>(e =>
        {
            e.ToTable("StockMovements", t => t.HasTrigger("TR_StockMovements_Immutable"));
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.PartId, x.CreatedAt });
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", t => t.HasTrigger("TR_AuditLogs_Immutable"));
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<SupportTicket>(e =>
        {
            e.ToTable("SupportTickets");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId);
        });

        b.Entity<LeaveRequest>(e =>
        {
            e.ToTable("LeaveRequests");
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        // SQL Server'da çoklu cascade yolu hatasını ve yanlışlıkla toplu silmeyi önlemek için hiçbir FK cascade değildir.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    /// <summary>Uygulama katmanında ikinci savunma: audit ve hareket kayıtları değiştirilemez/silinemez.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var illegal = ChangeTracker.Entries().Any(e =>
            (e.Entity is AuditLog or StockMovement) && (e.State is EntityState.Modified or EntityState.Deleted));
        if (illegal) throw new InvalidOperationException("Audit ve stok hareket kayıtları değiştirilemez veya silinemez.");
        return base.SaveChangesAsync(ct);
    }
}
