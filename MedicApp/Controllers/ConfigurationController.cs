using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MedicApp.Controllers;

[Authorize(Policy = "Access")]
public class ConfigurationController(ApplicationConfiguration settings, DemoDataService demo, MedicalAccess access, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await settings.ReadAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Index(ConfigurationForm input, CancellationToken ct)
    {
        if (!input.Open24Hours && input.OpeningTime == input.ClosingTime) { ModelState.AddModelError(nameof(input.ClosingTime), text["InvalidHours"]); }
        if (!ModelState.IsValid) { return View(input); }
        await settings.SaveAsync(input, ct);
        TempData["Success"] = "ConfigurationSaved";
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public IActionResult Demo() => View();
    [HttpGet] public async Task<IActionResult> DemoEvents(DateOnly from, DateOnly to, CancellationToken ct) => Json(await demo.EventsAsync(from, to, ct));
    [HttpPost]
    public async Task<IActionResult> GenerateDemo(CancellationToken ct)
    {
        await demo.CreateAsync(access.UserId, ct);
        TempData["Success"] = "DemoCreated";
        return RedirectToAction(nameof(Demo));
    }
}
