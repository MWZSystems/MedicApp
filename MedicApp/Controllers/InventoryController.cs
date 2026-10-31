using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Trainify.Data;
namespace MedicApp.Controllers;

[Authorize(Policy = "Inventory")]
public class InventoryController(MedicDbContext db, TrainifyDbContext shared, InventoryService inventory, IStringLocalizer<UiText> text) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, int page = 1, CancellationToken ct = default)
    {
        if (query?.Length > 80) { return BadRequest(); }
        page = Math.Clamp(page, 1, 100000); query = query?.Trim();
        var items = db.StockItems.AsNoTracking().Where(x => x.Available);
        if (!string.IsNullOrEmpty(query)) { items = items.Where(x => x.Code.Contains(query) || x.Name.Contains(query) || x.Lot.Contains(query)); }
        return View(new ListPage<Models.Entities.StockItem> { Page = page, Query = query ?? "", Total = await items.CountAsync(ct), Items = await items.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct) });
    }
    [HttpGet]
    public IActionResult Create() => View(new StockItemForm());
    [HttpPost]
    public async Task<IActionResult> Create(StockItemForm input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try { var id = await inventory.CreateAsync(input, ct); TempData["Success"] = "Saved"; return RedirectToAction(nameof(Item), new { id }); }
            catch (MedicalRuleException error) when (error.Message is "InvalidStockInput" or "Duplicate") { ModelState.AddModelError("", text[error.Message]); }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; return View(input);
    }
    private async Task<StockItemPage> PageAsync(int id, int page, StockMovementForm? input, CancellationToken ct)
    {
        var item = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        page = Math.Clamp(page, 1, 100000); var movements = db.StockMovements.AsNoTracking().Where(x => x.StockItemId == id);
        var result = new StockItemPage { Item = item, Form = input ?? new StockMovementForm { StockItemId = id }, Movements = new() { Page = page, Total = await movements.CountAsync(ct), Items = await movements.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct) } };
        var users = result.Movements.Items.Select(x => x.UpdatedById).Distinct().ToArray();
        result.AccountNames = await shared.Users.AsNoTracking().Where(x => users.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? "", ct);
        return result;
    }
    [HttpGet]
    public async Task<IActionResult> Item(int id, int page = 1, CancellationToken ct = default) => View(await PageAsync(id, page, null, ct));
    [HttpPost]
    public async Task<IActionResult> Move(StockMovementForm input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try { await inventory.MoveAsync(input, ct); TempData["Success"] = "Saved"; return RedirectToAction(nameof(Item), new { id = input.StockItemId }); }
            catch (MedicalRuleException error) when (error.Message is "InvalidStockInput" or "StockInsufficient" or "StockExpired") { ModelState.AddModelError("", text[error.Message]); }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; return View("Item", await PageAsync(input.StockItemId, 1, input, ct));
    }
}
