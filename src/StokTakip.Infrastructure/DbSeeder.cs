using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StokTakip.Application;
using StokTakip.Domain;

namespace StokTakip.Infrastructure;

/// <summary>Şartnamedeki örnek hiyerarşi, parça kodları ve parti verisiyle demo veri üretir. Veritabanı doluysa hiçbir şey yapmaz.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration cfg, ILogger logger)
    {
        if (!await db.Database.CanConnectAsync()) throw new InvalidOperationException("Veritabanına bağlanılamadı. database/01_schema.sql çalıştırıldı mı?");
        if (await db.Users.AnyAsync()) return;

        var pwd = cfg["Seed:Password"];
        if (string.IsNullOrWhiteSpace(pwd)) { logger.LogWarning("Seed:Password tanımlı değil; başlangıç verisi oluşturulmadı."); return; }
        var hash = hasher.Hash(pwd);

        // Kullanıcılar
        var mudur = new User { Username = "mudur", FullName = "Birim Müdürü", PasswordHash = hash, Role = Role.UnitManager };
        db.Users.AddRange(
            new User { Username = "admin", FullName = "Süper Admin", PasswordHash = hash, Role = Role.SuperAdmin },
            new User { Username = "itadmin", FullName = "IT Admin", PasswordHash = hash, Role = Role.ItAdmin },
            new User { Username = "depo", FullName = "Depo Sorumlusu", PasswordHash = hash, Role = Role.WarehouseManager },
            mudur,
            new User { Username = "personel", FullName = "Personel", PasswordHash = hash, Role = Role.Staff, Manager = mudur });

        // Depolar
        var central = new Warehouse { Name = "Merkez Depo", Type = WarehouseType.Central };
        db.Warehouses.AddRange(central,
            new Warehouse { Name = "Bölge Servis Deposu", Type = WarehouseType.Regional },
            new Warehouse { Name = "Üretim Montaj Hattı", Type = WarehouseType.ProductionLine });

        // Kategori ağacı (PDF bölüm 5)
        Category C(string name, Category? parent = null) { var c = new Category { Name = name, Parent = parent }; db.Categories.Add(c); return c; }
        var root = C("Cihaz ve Parça Hiyerarşisi");
        var phones = C("Akıllı Telefonlar", root); var sSeries = C("S Serisi", phones); var s19 = C("S19 Modeli", sSeries);
        var s19Parts = C("Donanım Parçaları", s19); C("S23 Modeli", sSeries);
        var p13 = C("P13 Modeli", C("P Serisi", phones)); var p13Parts = C("Bileşenler", p13);
        var tabletPanel = C("10.1 inç Seri", C("Tabletler", root));
        var acc = C("Aksesuarlar", root); C("Akıllı Saat", acc);

        var seq = 0;
        Part P(string code, string name, Category cat, int min, bool lots, decimal buy, decimal sell) =>
            new() { Code = code, Barcode = "8690000" + (++seq).ToString("D6"), Name = name, Category = cat, MinStock = min, TracksLots = lots, PurchasePrice = buy, SalePrice = sell };
        var bat = P("RDR-S19P-BAT-002", "Batarya 5000mAh", s19Parts, 100, true, 120, 180);
        db.Parts.AddRange(
            P("RDR-S19P-MB-001", "Anakart", s19Parts, 20, false, 900, 1400), bat,
            P("RDR-S19P-LCD-003", "Ekran / LCD Panel", s19Parts, 30, false, 450, 700),
            P("RDR-S19P-CAM-004", "Kamera Modülü", s19Parts, 30, false, 200, 320),
            P("RDR-P13B-BAT-010", "Batarya 4000mAh", p13Parts, 50, true, 90, 140),
            P("RDR-P13B-IC-011", "Şarj Entegresi", p13Parts, 40, false, 25, 45),
            P("RDR-RP10-TP-101", "Dokunmatik Panel", tabletPanel, 25, false, 150, 260),
            P("RDR-ACC-CHG-25W", "25W Hızlı Şarj Adaptörü", acc, 200, false, 60, 110));
        await db.SaveChangesAsync();

        // PDF bölüm 7: üç parti (tarihler bugüne göre ayarlandı ki uyarı seviyeleri anlamlı olsun)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var admin = await db.Users.FirstAsync(u => u.Username == "admin");
        foreach (var (no, days, qty) in new[] { ("LOT-2026-01", 25, 40), ("LOT-2026-05", 80, 150), ("LOT-2026-09", 300, 300) })
        {
            var lot = new StockLot { PartId = bat.Id, WarehouseId = central.Id, LotNumber = no, ExpiryDate = today.AddDays(days), ReceivedAt = DateTime.UtcNow };
            lot.Apply(qty); bat.ApplyDelta(qty);
            db.StockLots.Add(lot);
            db.StockMovements.Add(new StockMovement { Part = bat, WarehouseId = central.Id, Lot = lot, Quantity = qty, Type = MovementType.GoodsReceipt, Note = "Seed - mal kabul", UserId = admin.Id, CreatedAt = DateTime.UtcNow });
        }
        db.AuditLogs.Add(new AuditLog { Action = "DatabaseSeeded", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        logger.LogInformation("Başlangıç verisi oluşturuldu.");
    }
}
