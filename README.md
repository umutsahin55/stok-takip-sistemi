# Stok Takip Sistemi

Cihaz, aksesuar ve yedek parçaların depoya girişinden üretim/servis çıkışına kadar olan akışını yöneten, katmanlı mimariye sahip ASP.NET Core Web API.

## Teknoloji
.NET 8 · ASP.NET Core Web API · EF Core 8 (SQL Server) · FluentValidation · BCrypt.Net · JWT (HttpOnly cookie veya Bearer) · Swagger

## Mimari
```
src/
├─ StokTakip.Domain          Entity'ler, enum'lar, iş değişmezleri (Part.ApplyDelta, StockLot.Apply)
├─ StokTakip.Application     Servisler (iş mantığı), DTO'lar, FluentValidation, arayüzler (IRepository, IUnitOfWork ...)
├─ StokTakip.Infrastructure  EF DbContext, Repository + Unit of Work, BCrypt, JWT, seed
└─ StokTakip.Api             Controller'lar, auth/RBAC, middleware (hata, güvenlik başlıkları, CSRF, rate limit)
database/01_schema.sql       SQL Server kurulum scripti
```
Bağımlılık yönü: Api → Infrastructure → Application → Domain. Tüm bağımlılıklar DI ile yönetilir; servisler yalnızca arayüzlere bağımlıdır.

## Kurulum
Gereksinimler: **.NET 8 SDK**, **Docker** (veya yerel bir SQL Server 2019+). Komutlar macOS/Linux içindir; Windows'ta `export` yerine PowerShell'de `$env:ASPNETCORE_ENVIRONMENT = "Development"` kullanın.

Aşağıdaki komutları proje kök klasöründen (`README.md`'nin bulunduğu yer) çalıştırın.

**1) SQL Server'ı Docker ile başlatın**
```bash
docker run --name sqlserver \
  -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=Guclu.Parola123!' \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```
- Parola büyük/küçük harf, rakam ve özel karakter içermelidir; aksi halde konteyner kapanır. Kendi parolanızı seçerseniz aşağıdaki komutlarda da aynısını kullanın.
- **Apple Silicon (M1/M2/M3/M4):** İmaj x86 olduğu için Docker Desktop → Settings → General → *"Use Rosetta for x86_64/amd64 emulation"* açık olmalı; ilk açılış 30-60 sn sürebilir. Hazır olduğunu şununla doğrulayın: `docker logs sqlserver 2>&1 | tail -n 5` (çıktıda *ready for client connections* yazmalı).
- Zaten `sqlserver` adlı bir konteyner varsa: `docker start sqlserver` veya `docker rm -f sqlserver` ile silip yeniden oluşturun.

**2) Veritabanı şemasını kurun**
```bash
docker cp database/01_schema.sql sqlserver:/tmp/01_schema.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Guclu.Parola123!' -C -i /tmp/01_schema.sql
```
Hata mesajı çıkmazsa tamamdır. (SSMS/Azure Data Studio kullanıyorsanız `database/01_schema.sql` dosyasını doğrudan çalıştırabilirsiniz.)

**3) Bağlantı dizesini verin (repoya yazılmaz, user-secrets'te tutulur)**
```bash
cd src/StokTakip.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=StokTakipDb;User Id=sa;Password=Guclu.Parola123!;TrustServerCertificate=True"
```
`Jwt:Key` ve `Seed:Password` Development ortamında `appsettings.Development.json` içinde hazırdır (**yalnızca geliştirme içindir**). Üretimde `Jwt__Key` (en az 32 karakter) ve `Seed__Password` ortam değişkenleriyle verilmelidir.

**4) Çalıştırın** (`src/StokTakip.Api` klasöründe)
```bash
dotnet dev-certs https --trust        # bir kez; cookie Secure olduğu için https gerekir
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --urls "https://localhost:7001"
```
Swagger: **https://localhost:7001/swagger**

> Visual Studio / VS Code'da solution olarak açmak isterseniz kök klasörde bir kez:
> `dotnet new sln -n StokTakip && dotnet sln add src/StokTakip.Domain src/StokTakip.Application src/StokTakip.Infrastructure src/StokTakip.Api`

İlk açılışta veritabanı boşsa **seed** çalışır: 5 rol için kullanıcı, 3 depo, PDF'teki kategori ağacı ve parça kodları, `RDR-S19P-BAT-002` için 3 partili örnek stok.

Demo kullanıcılar (parola: `Seed:Password`, Development'ta `Demo!Pass2026#`):
`admin` (SuperAdmin) · `itadmin` · `depo` (Depo Sorumlusu) · `mudur` (Birim Müdürü) · `personel` (yöneticisi `mudur`)

> Bu parolalar ve `appsettings.Development.json` içindeki JWT anahtarı yalnızca yerel demo içindir; gerçek ortamda kullanılmamalıdır.

## Hızlı deneme (Swagger/Postman)
1. `POST /api/auth/login` → Development'ta yanıt gövdesinde `token` döner; `eyJ...` ile başlayan değeri (tırnaksız) kopyalayıp Swagger'da **Authorize** kutusuna yapıştırın. Kullanıcı değiştirmek için Authorize → Logout → yeni token. (Tarayıcı istemcisi HttpOnly cookie kullanır; üretimde gövdede token dönmez.)
2. `GET /api/dashboard` → konsolide özet.
3. `POST /api/stock/move` (örnek):
```json
{ "partId": 1, "warehouseId": 1, "quantity": 50, "type": "ProductionIssue", "note": "Hat-1" }
```
`partId` değerini `GET /api/parts?q=BAT-002`, `warehouseId` değerini `GET /api/stock/warehouses` ile öğrenin. FEFO'ya göre en yakın SKT'li parti(ler)den düşer; yanıt hangi partiden kaç adet çıkıldığını gösterir.

## Şartname → Uygulama eşlemesi
| # | Gereksinim | Nerede |
|---|---|---|
| 1 | Dashboard, kritik stok, yaklaşan SKT (90/60/30), son hareketler | `DashboardService`, `GET /api/dashboard` |
| 2 | Katmanlı mimari, Repository, Unit of Work, DTO, FluentValidation, DI | Çözüm yapısı, `Persistence.cs`, `Validators.cs`, `ValidationFilter` |
| 3 | BCrypt, JWT/HttpOnly cookie, logout, no-store, SQLi/XSS/CSRF/IDOR | `Security.cs`, `Program.cs`, `Middleware.cs` (aşağıya bakın) |
| 4 | RBAC (backend endpoint seviyesinde) | `[Authorize(Roles=...)]` + `FallbackPolicy` (varsayılan: kimlik doğrulama zorunlu), `Roles` sınıfı |
| 5 | Sınırsız derinlikte kategori ağacı | `Category` (self-reference), `CategoryService` (döngü koruması, alt ağaç sorgusu) |
| 6 | Benzersiz kod/barkod, parça kartı, stok elle değiştirilemez | DB `UNIQUE`, `PartService`, `Part.CurrentStock` private setter |
| 7 | Parti + SKT, FEFO/FIFO | `StockLot`, `StockService.MoveAsync` |
| 8 | Stok hareket geçmişi | `StockMovement` (append-only), `GET /api/stock/movements` |
| 9 | Destek talepleri, Personel→Yönetici→İK izin onayı | `SupportService`, `LeaveService` |
| 10 | Denetim logu (zaman, IP, kullanıcı, eski/yeni JSON) | `AuditLogger`, `AuditLog` (append-only), `GET /api/audit-logs` |

### Güvenlik notları
- **SQL Injection:** Ham SQL yok; tüm sorgular EF Core ile parametreli.
- **IDOR:** Kayıt sahibi kontrolü servis katmanında (`SupportService.Visible()`, `LeaveService.DecideAsync`); yetkisiz erişimde 404/403. Parça/kategori gibi paylaşılan kaynaklar rol ile korunur.
- **XSS:** API yalnızca JSON döner; `Content-Security-Policy: default-src 'none'`, `nosniff`. İstemci tarafında çıktı encode edilmelidir.
- **CSRF:** Cookie `HttpOnly + Secure + SameSite=Strict`; ayrıca cookie ile gelen state-değiştiren isteklerde `X-Requested-With: XMLHttpRequest` başlığı zorunlu.
- **Oturum:** Logout / rol değişimi / parola sıfırlama / pasifleştirmede `User.TokenVersion` artırılır; JWT bir sonraki istekte anında reddedilir.
- **Brute force:** Login için IP başına 5 istek/dakika; başarısız girişler audit loglanır; kullanıcı yokken de hash doğrulaması yapılır.
- **Değişmezlik:** `AuditLogs` ve `StockMovements` için hem `DbContext.SaveChangesAsync` kontrolü hem DB `INSTEAD OF UPDATE/DELETE` trigger'ı.
- **Eşzamanlılık:** `Part` ve `StockLot` üzerinde `rowversion`; çakışmada 409.

## Varsayımlar / belirsizlikler (şartnamedeki boşluklar)
- **İK rolü** şartnamedeki rol listesinde yok; İK onayı `SuperAdmin` tarafından verilir.
- **Kritik/azaldı eşiği:** `stok ≤ min` → *Kritik Stok*, `stok ≤ min×1.5` → *Stok Azaldı*.
- **İade (+) / "Arızalı parça iadesi (−)":** PDF'te çelişkili. Müşteri/üretim iadesi `Return` (+), arızalı/kusurlu düşüş `Damaged` (−) olarak modellendi.
- **Süresi dolmuş partiler** çıkışta atlanır (yalnızca `Damaged` düşüşte seçilebilir). Yeterli geçerli stok yoksa 409 döner.
- **Parti takibi olmayan parçalar** tek bir `DEFAULT` lot ile izlenir; böylece depo bazlı stok tek modelde tutulur.
- PDF'teki örnekte aynı parçanın toplamı bir yerde 490, diğerinde 315 görünüyor; bunlar farklı örnek senaryolar kabul edildi. PDF'teki parti tarihleri (01/2026 vb.) bugüne göre geçmişte kaldığından seed tarihleri göreli üretilir.

## Kapsam dışı / sonraki adımlar
Birim/entegrasyon testleri, EF Core migration'ları (şema şimdilik `01_schema.sql` ile), depolar arası transfer (iki bacaklı hareket), parola unutma akışı, ön yüz.
