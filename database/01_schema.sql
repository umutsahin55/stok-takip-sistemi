/* Stok Takip Sistemi - SQL Server şema scripti (EF Core modeli ile birebir uyumludur).
   Çalıştırma:  sqlcmd -S localhost -U sa -P "<parola>" -C -i database/01_schema.sql
   Başlangıç verisi (kullanıcılar, depolar, kategori ağacı, örnek parçalar/partiler) uygulama ilk açılışında seed edilir. */
IF DB_ID(N'StokTakipDb') IS NULL CREATE DATABASE StokTakipDb;
GO
USE StokTakipDb;
GO

CREATE TABLE Users (
    Id            INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    Username      NVARCHAR(50)  NOT NULL,
    FullName      NVARCHAR(100) NOT NULL,
    PasswordHash  NVARCHAR(100) NOT NULL,               -- BCrypt
    Role          INT           NOT NULL,               -- 1 SuperAdmin,2 ItAdmin,3 WarehouseManager,4 UnitManager,5 Staff
    ManagerId     INT           NULL,
    IsActive      BIT           NOT NULL,
    TokenVersion  INT           NOT NULL,
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT FK_Users_Manager FOREIGN KEY (ManagerId) REFERENCES Users(Id),
    CONSTRAINT CK_Users_Role CHECK (Role BETWEEN 1 AND 5)
);

CREATE TABLE Warehouses (
    Id       INT IDENTITY(1,1) CONSTRAINT PK_Warehouses PRIMARY KEY,
    Name     NVARCHAR(100) NOT NULL,
    Type     INT NOT NULL,                              -- 1 Merkez, 2 Bölge, 3 Üretim hattı
    IsActive BIT NOT NULL
);

CREATE TABLE Categories (                               -- self-referencing: sınırsız derinlik
    Id       INT IDENTITY(1,1) CONSTRAINT PK_Categories PRIMARY KEY,
    Name     NVARCHAR(150) NOT NULL,
    ParentId INT NULL,
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES Categories(Id)
);
CREATE INDEX IX_Categories_ParentId ON Categories(ParentId);

CREATE TABLE Parts (
    Id            INT IDENTITY(1,1) CONSTRAINT PK_Parts PRIMARY KEY,
    Code          NVARCHAR(50)  NOT NULL,
    Barcode       NVARCHAR(50)  NOT NULL,
    Name          NVARCHAR(200) NOT NULL,
    CategoryId    INT NOT NULL,
    Unit          NVARCHAR(20)  NOT NULL,
    PurchasePrice DECIMAL(18,2) NOT NULL,
    SalePrice     DECIMAL(18,2) NOT NULL,
    MinStock      INT NOT NULL,
    IsActive      BIT NOT NULL,
    TracksLots    BIT NOT NULL,
    IssuePolicy   INT NOT NULL,                         -- 1 FEFO, 2 FIFO
    CurrentStock  INT NOT NULL,                         -- yalnızca stok hareketleriyle güncellenir
    RowVersion    ROWVERSION NOT NULL,
    CONSTRAINT UQ_Parts_Code    UNIQUE (Code),          -- mükerrer parça kodu engeli
    CONSTRAINT UQ_Parts_Barcode UNIQUE (Barcode),       -- mükerrer barkod engeli
    CONSTRAINT FK_Parts_Category FOREIGN KEY (CategoryId) REFERENCES Categories(Id),
    CONSTRAINT CK_Parts_Stock CHECK (CurrentStock >= 0 AND MinStock >= 0)
);
CREATE INDEX IX_Parts_CategoryId ON Parts(CategoryId);

CREATE TABLE StockLots (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_StockLots PRIMARY KEY,
    PartId         INT NOT NULL,
    WarehouseId    INT NOT NULL,
    LotNumber      NVARCHAR(50) NOT NULL,               -- parti takibi olmayan parçalar için 'DEFAULT'
    ProductionDate DATE NULL,
    ExpiryDate     DATE NULL,                           -- SKT / garanti bitişi
    ReceivedAt     DATETIME2 NOT NULL,
    Quantity       INT NOT NULL,
    RowVersion     ROWVERSION NOT NULL,
    CONSTRAINT FK_StockLots_Part      FOREIGN KEY (PartId)      REFERENCES Parts(Id),
    CONSTRAINT FK_StockLots_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
    CONSTRAINT UQ_StockLots_Part_Wh_Lot UNIQUE (PartId, WarehouseId, LotNumber),
    CONSTRAINT CK_StockLots_Qty CHECK (Quantity >= 0)
);
CREATE INDEX IX_StockLots_Part_Wh_Expiry ON StockLots(PartId, WarehouseId, ExpiryDate);

CREATE TABLE StockMovements (                           -- append-only (trigger ile korunur)
    Id          BIGINT IDENTITY(1,1) CONSTRAINT PK_StockMovements PRIMARY KEY,
    PartId      INT NOT NULL,
    WarehouseId INT NOT NULL,
    LotId       INT NULL,
    Quantity    INT NOT NULL,                           -- işaretli: + giriş, - çıkış
    Type        INT NOT NULL,                           -- 1 MalKabul,2 ÜretimÇıkış,3 ServisTransfer,4 İade,5 Hasarlı
    Note        NVARCHAR(500) NULL,
    UserId      INT NOT NULL,
    CreatedAt   DATETIME2 NOT NULL,
    CONSTRAINT FK_StockMovements_Part      FOREIGN KEY (PartId)      REFERENCES Parts(Id),
    CONSTRAINT FK_StockMovements_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
    CONSTRAINT FK_StockMovements_Lot       FOREIGN KEY (LotId)       REFERENCES StockLots(Id),
    CONSTRAINT FK_StockMovements_User      FOREIGN KEY (UserId)      REFERENCES Users(Id),
    CONSTRAINT CK_StockMovements_Qty CHECK (Quantity <> 0)
);
CREATE INDEX IX_StockMovements_Part_CreatedAt ON StockMovements(PartId, CreatedAt);
CREATE INDEX IX_StockMovements_LotId ON StockMovements(LotId);
CREATE INDEX IX_StockMovements_WarehouseId ON StockMovements(WarehouseId);
CREATE INDEX IX_StockMovements_UserId ON StockMovements(UserId);

CREATE TABLE AuditLogs (                                -- append-only (trigger ile korunur)
    Id         BIGINT IDENTITY(1,1) CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId     INT NULL,
    Action     NVARCHAR(100) NOT NULL,
    EntityName NVARCHAR(100) NULL,
    EntityId   NVARCHAR(100) NULL,
    IpAddress  NVARCHAR(64)  NULL,
    OldValues  NVARCHAR(MAX) NULL,                      -- JSON
    NewValues  NVARCHAR(MAX) NULL,                      -- JSON
    CreatedAt  DATETIME2 NOT NULL
);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt);

CREATE TABLE SupportTickets (
    Id           INT IDENTITY(1,1) CONSTRAINT PK_SupportTickets PRIMARY KEY,
    Title        NVARCHAR(200)  NOT NULL,
    Description  NVARCHAR(4000) NOT NULL,
    Category     INT NOT NULL,                          -- 1 Donanım,2 Ağ,3 ERP,4 Yazılım
    Status       INT NOT NULL,                          -- 1 Açık,2 İşlemde,3 Bekliyor,4 Tamamlandı
    CreatedById  INT NOT NULL,
    AssignedToId INT NULL,
    CreatedAt    DATETIME2 NOT NULL,
    UpdatedAt    DATETIME2 NOT NULL,
    CONSTRAINT FK_SupportTickets_CreatedBy  FOREIGN KEY (CreatedById)  REFERENCES Users(Id),
    CONSTRAINT FK_SupportTickets_AssignedTo FOREIGN KEY (AssignedToId) REFERENCES Users(Id)
);
CREATE INDEX IX_SupportTickets_CreatedById ON SupportTickets(CreatedById);
CREATE INDEX IX_SupportTickets_AssignedToId ON SupportTickets(AssignedToId);

CREATE TABLE LeaveRequests (
    Id                  INT IDENTITY(1,1) CONSTRAINT PK_LeaveRequests PRIMARY KEY,
    UserId              INT NOT NULL,
    StartDate           DATE NOT NULL,
    EndDate             DATE NOT NULL,
    Reason              NVARCHAR(500) NOT NULL,
    Status              INT NOT NULL,                   -- 1 YöneticiBekliyor,2 İKBekliyor,3 Onaylandı,4 Reddedildi
    ManagerApprovedById INT NULL,
    HrApprovedById      INT NULL,
    CreatedAt           DATETIME2 NOT NULL,
    CONSTRAINT FK_LeaveRequests_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_LeaveRequests_Dates CHECK (EndDate >= StartDate)
);
CREATE INDEX IX_LeaveRequests_UserId ON LeaveRequests(UserId);
GO

/* Değişmez kayıtlar: UPDATE/DELETE veritabanı seviyesinde reddedilir. */
CREATE TRIGGER TR_AuditLogs_Immutable ON AuditLogs INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 50001, N'AuditLogs tablosu değiştirilemez veya silinemez.', 1;
END
GO
CREATE TRIGGER TR_StockMovements_Immutable ON StockMovements INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 50002, N'StockMovements tablosu değiştirilemez veya silinemez.', 1;
END
GO
