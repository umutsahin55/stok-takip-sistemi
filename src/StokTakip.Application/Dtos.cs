using StokTakip.Domain;

namespace StokTakip.Application;

public record PagedResult<T>(List<T> Items, int Total, int Page, int Size);

// Auth & kullanıcı
public record LoginDto(string Username, string Password);
public record LoginResultDto(string Token, DateTime ExpiresAt, int UserId, string Username, string FullName, Role Role);
public record UserDto(int Id, string Username, string FullName, Role Role, int? ManagerId, bool IsActive);
public record CreateUserDto(string Username, string FullName, string Password, Role Role, int? ManagerId);
public record ChangeRoleDto(Role Role);
public record ResetPasswordDto(string NewPassword);
public record SetActiveDto(bool IsActive);

// Kategori & parça
public record CategoryCreateDto(string Name, int? ParentId);
public record CategoryMoveDto(int? NewParentId);
public record CategoryNodeDto(int Id, string Name, int? ParentId, List<CategoryNodeDto> Children);
public record PartUpsertDto(string Code, string Barcode, string Name, int CategoryId, string Unit,
    decimal PurchasePrice, decimal SalePrice, int MinStock, bool IsActive, bool TracksLots, IssuePolicy IssuePolicy);
public record PartDto(int Id, string Code, string Barcode, string Name, int CategoryId, string CategoryPath, string Unit,
    decimal PurchasePrice, decimal SalePrice, int MinStock, int CurrentStock, bool IsActive, bool TracksLots, IssuePolicy IssuePolicy);

// Stok
public record MoveStockDto(int PartId, int WarehouseId, int Quantity, MovementType Type,
    string? LotNumber, DateOnly? ProductionDate, DateOnly? ExpiryDate, string? Note);
public record MovementLineDto(string LotNumber, int Quantity);
public record StockResultDto(int PartId, int CurrentStock, List<MovementLineDto> Lines);
public record MovementDto(long Id, int PartId, string PartCode, string Warehouse, string? LotNumber,
    int Quantity, MovementType Type, string? Note, string User, DateTime CreatedAt);
public record LotDto(int Id, string Warehouse, string LotNumber, DateOnly? ProductionDate, DateOnly? ExpiryDate, int Quantity, string Status);
public record PartLotsDto(int PartId, string Code, string Name, IssuePolicy IssueMethod, int TotalStock, List<LotDto> Lots);
public record WarehouseDto(int Id, string Name, WarehouseType Type);

// Dashboard
public record CriticalPartDto(int Id, string Code, string Name, int CurrentStock, int MinStock, string Level);
public record ExpiringLotDto(string PartCode, string PartName, string Warehouse, string LotNumber, DateOnly ExpiryDate, int DaysLeft, string Level, int Quantity);
public record WarehouseStockDto(string Warehouse, int Total);
public record DashboardDto(int TotalProducts, int TotalStock, int CriticalCount, int LowCount,
    List<CriticalPartDto> CriticalParts, List<ExpiringLotDto> ExpiringLots,
    List<WarehouseStockDto> StockByWarehouse, List<MovementDto> RecentMovements);

// Destek & izin
public record TicketCreateDto(string Title, string Description, TicketCategory Category);
public record TicketStatusDto(TicketStatus Status, int? AssignedToId);
public record TicketDto(int Id, string Title, string Description, TicketCategory Category, TicketStatus Status,
    string CreatedBy, string? AssignedTo, DateTime CreatedAt, DateTime UpdatedAt);
public record LeaveCreateDto(DateOnly StartDate, DateOnly EndDate, string Reason);
public record LeaveDecisionDto(bool Approve);
public record LeaveDto(int Id, string Applicant, DateOnly StartDate, DateOnly EndDate, string Reason, LeaveStatus Status, DateTime CreatedAt);

public record AuditLogDto(long Id, int? UserId, string Action, string? EntityName, string? EntityId,
    string? IpAddress, string? OldValues, string? NewValues, DateTime CreatedAt);
