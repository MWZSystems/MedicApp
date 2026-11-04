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
public class InventoryController(MedicDbContext db, TrainifyDbContext shared, InventoryService inventory, MedicalClock clock, Microsoft.Extensions.Options.IOptions<InventoryOptions> settings, IStringLocalizer<UiText> text) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? query, int page = 1, CancellationToken ct = default)
    {
        query = this.Search();
        var items = db.StockItems.AsNoTracking();
        var active = this.Value("active"); if (active != "all") { var available = active != "false"; items = items.Where(x => x.Available == available); }
        if (query != null) { items = items.Where(x => x.Code.Contains(query) || x.Name.Contains(query) || x.Lot.Contains(query)); }
        items = this.Value("stock") switch { "low" => items.Where(x => x.CurrentStock <= x.MinimumStock), "zero" => items.Where(x => x.CurrentStock == 0), "positive" => items.Where(x => x.CurrentStock > 0), _ => items };
        var today = DateOnly.FromDateTime(clock.Local(clock.UtcNow)); var soon = today.AddDays(settings.Value.ExpiryWarningDays);
        items = this.Value("expiry") switch { "expired" => items.Where(x => x.ExpiresOn < today), "soon" => items.Where(x => x.ExpiresOn >= today && x.ExpiresOn <= soon), "valid" => items.Where(x => x.ExpiresOn == null || x.ExpiresOn >= today), _ => items };
        this.Filters(ListFiltering.Choice("active", "ListAvailability", ("all", "ListAll"), ("true", "ListActive"), ("false", "ListInactive")) with { DefaultValue = "true" }, ListFiltering.Choice("stock", "ListStock", ("low", "LowStock"), ("zero", "ListZero"), ("positive", "ListPositive")), ListFiltering.Choice("expiry", "ListExpiry", ("expired", "Expired"), ("soon", "ListSoonExpiry"), ("valid", "ListValid")));
        var rows = await items.OrderBy(x => x.Name).ThenBy(x => x.Id).ToPageAsync(this);
        var info = (PageInfo)ViewData[Paging.ViewDataKey()]!;
        return View(new ListPage<Models.Entities.StockItem> { Page = info.Number, Query = query ?? "", Total = info.Total, Items = rows });
    }

    [HttpGet]
    public IActionResult Create() => View(new StockItemForm());
    [HttpPost]
    public async Task<IActionResult> Create(StockItemForm input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try { var id = await inventory.CreateAsync(input, ct); this.Success("FeedbackStockCreated"); return RedirectToAction(nameof(Item), new { id }); }
            catch (MedicalRuleException error) when (error.Message is "InvalidStockInput" or "Duplicate") { ModelState.AddModelError("", text[error.Message]); }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; return View(input);
    }
    private async Task<StockItemPage> PageAsync(int id, int page, StockMovementForm? input, CancellationToken ct)
    {
        var item = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new MedicalRuleException("NotFound");
        var movements = db.StockMovements.AsNoTracking().Where(x => x.StockItemId == id).Dates(this, clock);
        var search = this.Search(); if (search != null) { var accounts = ListFiltering.Accounts(db, search); movements = movements.Where(x => x.Reason.Contains(search) || accounts.Contains(x.UpdatedById)); }
        if (int.TryParse(this.Value("kind"), out var kind) && Enum.IsDefined(typeof(Models.Entities.StockMovementKind), kind)) { movements = movements.Where(x => (int)x.Kind == kind); }
        this.Filters(ListFiltering.Choice("kind", "MovementKind", Enum.GetValues<Models.Entities.StockMovementKind>().Select(x => (((int)x).ToString(System.Globalization.CultureInfo.InvariantCulture), "Stock" + x)).ToArray()), new("from", "From", Date: true), new("to", "To", Date: true));
        var rows = await movements.OrderByDescending(x => x.Id).ToPageAsync(this); var info = (PageInfo)ViewData[Paging.ViewDataKey()]!;
        var result = new StockItemPage { Item = item, Form = input ?? new StockMovementForm { StockItemId = id }, Movements = new() { Page = info.Number, Total = info.Total, Items = rows } };
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
            try
            {
                await inventory.MoveAsync(input, ct); this.Success("FeedbackStockMoved"); return this.RedirectBack(nameof(Item), new { id = input.StockItemId });
            }
            catch (MedicalRuleException error) when (error.Message is "InvalidStockInput" or "StockInsufficient" or "StockExpired") { ModelState.AddModelError("", text[error.Message]); }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; return View("Item", await PageAsync(input.StockItemId, 1, input, ct));
    }
}
