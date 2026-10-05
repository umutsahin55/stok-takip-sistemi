using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StokTakip.Api.Infrastructure;
using StokTakip.Application;
using StokTakip.Application.Services;

namespace StokTakip.Api.Controllers;

[ApiController, Route("api/categories"), Authorize(Roles = Roles.StockRead)]
public class CategoriesController(CategoryService svc) : ControllerBase
{
    [HttpGet("tree")] public async Task<IActionResult> Tree(CancellationToken ct) => Ok(await svc.GetTreeAsync(ct));

    [HttpPost, Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Create(CategoryCreateDto dto, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}/parent"), Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Move(int id, CategoryMoveDto dto, CancellationToken ct) { await svc.MoveAsync(id, dto.NewParentId, ct); return NoContent(); }

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Route("api/parts"), Authorize(Roles = Roles.StockRead)]
public class PartsController(PartService svc, StockService stock) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Search(string? q, int? categoryId, int page = 1, int size = 20, CancellationToken ct = default) => Ok(await svc.SearchAsync(q, categoryId, page, size, ct));
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await svc.GetAsync(id, ct));
    [HttpGet("{id:int}/lots")] public async Task<IActionResult> Lots(int id, CancellationToken ct) => Ok(await stock.GetLotsAsync(id, ct));

    [HttpPost, Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Create(PartUpsertDto dto, CancellationToken ct) => StatusCode(201, await svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}"), Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Update(int id, PartUpsertDto dto, CancellationToken ct) => Ok(await svc.UpdateAsync(id, dto, ct));
}

[ApiController, Route("api/stock"), Authorize(Roles = Roles.StockRead)]
public class StockController(StockService svc) : ControllerBase
{
    [HttpPost("move"), Authorize(Roles = Roles.StockWrite)]
    public async Task<IActionResult> Move(MoveStockDto dto, CancellationToken ct) => Ok(await svc.MoveAsync(dto, ct));

    [HttpGet("movements")]
    public async Task<IActionResult> Movements(int? partId, int page = 1, int size = 50, CancellationToken ct = default) => Ok(await svc.GetMovementsAsync(partId, page, size, ct));

    [HttpGet("warehouses")] public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await svc.GetWarehousesAsync(ct));
}

[ApiController, Route("api/dashboard"), Authorize(Roles = Roles.StockRead)]
public class DashboardController(DashboardService svc) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(await svc.GetAsync(ct));
}
