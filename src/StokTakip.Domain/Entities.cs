namespace StokTakip.Domain;

public enum Role { SuperAdmin = 1, ItAdmin = 2, WarehouseManager = 3, UnitManager = 4, Staff = 5 }
public enum WarehouseType { Central = 1, Regional = 2, ProductionLine = 3 }
public enum IssuePolicy { FEFO = 1, FIFO = 2 }
public enum MovementType { GoodsReceipt = 1, ProductionIssue = 2, ServiceTransfer = 3, Return = 4, Damaged = 5 }
public enum TicketCategory { Hardware = 1, Network = 2, Erp = 3, Software = 4 }
public enum TicketStatus { Open = 1, InProgress = 2, Waiting = 3, Completed = 4 }
public enum LeaveStatus { PendingManager = 1, PendingHr = 2, Approved = 3, Rejected = 4 }

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Token'lardaki "tv" claim'i ile karşılaştırılır; artırılınca tüm oturumlar anında geçersiz olur.</summary>
    public int TokenVersion { get; set; }
}

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public WarehouseType Type { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Self-referencing (ağaç) kategori; derinlik sınırsızdır.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}

public class Part
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Unit { get; set; } = "Adet";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int MinStock { get; set; }
    public bool IsActive { get; set; } = true;
    public bool TracksLots { get; set; }
    public IssuePolicy IssuePolicy { get; set; } = IssuePolicy.FEFO;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>Elle set edilemez; yalnızca stok hareketleri (StockService) ApplyDelta ile günceller.</summary>
    public int CurrentStock { get; private set; }
    public void ApplyDelta(int delta)
    {
        if (CurrentStock + delta < 0) throw new InvalidOperationException("Stok negatife düşemez.");
        CurrentStock += delta;
    }
}

/// <summary>Parti bazlı stok. Parti takibi olmayan parçalar için "DEFAULT" lot kullanılır.</summary>
public class StockLot
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public string LotNumber { get; set; } = "";
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateTime ReceivedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public int Quantity { get; private set; }
    public void Apply(int delta)
    {
        if (Quantity + delta < 0) throw new InvalidOperationException("Parti stoğu negatife düşemez.");
        Quantity += delta;
    }
}

/// <summary>Append-only: değiştirilemez / silinemez (DbContext + DB trigger).</summary>
public class StockMovement
{
    public long Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int? LotId { get; set; }
    public StockLot? Lot { get; set; }
    public int Quantity { get; set; } // işaretli: + giriş, - çıkış
    public MovementType Type { get; set; }
    public string? Note { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Append-only denetim kaydı.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportTicket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TicketCategory Category { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LeaveRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";
    public LeaveStatus Status { get; set; }
    public int? ManagerApprovedById { get; set; }
    public int? HrApprovedById { get; set; }
    public DateTime CreatedAt { get; set; }
}
