using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MedicApp.Controllers;

[Authorize(Policy = "Access")]
public class ConfigurationController(ApplicationConfiguration settings, DemoDataService demo, MedicalAccess access, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    private ViewResult Page(ConfigurationForm model)
    {
        var ids = new[] { "America/Mexico_City", "America/Chihuahua", "America/Ciudad_Juarez", "America/Hermosillo", "America/Tijuana", "America/Monterrey", "America/Cancun", "UTC", model.General?.TimeZoneId };
        ViewBag.TimeZones = ids.Where(x => !string.IsNullOrWhiteSpace(x)).OfType<string>().Distinct(StringComparer.Ordinal).Where(x => TimeZoneInfo.TryFindSystemTimeZoneById(x, out _)).ToArray();
        return View("Index", model);
    }
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => Page(await settings.ReadAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Index(ConfigurationForm input, CancellationToken ct)
    {
        if (!input.Open24Hours && input.OpeningTime == input.ClosingTime) { ModelState.AddModelError(nameof(input.ClosingTime), text["InvalidHours"]); }
        if (ModelState.IsValid)
        {
            try
            {
                await settings.SaveAsync(input, ct); TempData["Success"] = "ConfigurationSaved";
                return RedirectToAction(nameof(Index));
            }
            catch (MedicalRuleException error) when (error.Message is "InvalidConfiguration" or "InvalidGeneralConfiguration" or "InvalidDocumentConfiguration" or "InvalidInventoryConfiguration" or "InvalidHours" or "Concurrency")
            {
                ModelState.AddModelError("", text[error.Message]);
                Response.StatusCode = error.Message == "Concurrency" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            }
        }
        else
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            foreach (var entry in ModelState.Values)
            {
                var errors = entry.Errors.Select(x => x.ErrorMessage).ToArray(); entry.Errors.Clear();
                foreach (var message in errors) { entry.Errors.Add(text[message]); }
            }
        }
        return Page(input);
    }
    [HttpGet] public IActionResult Demo() => View();
    [HttpGet] public async Task<IActionResult> DemoEvents(DateOnly from, DateOnly to, CancellationToken ct) => Json(await demo.EventsAsync(from, to, ct));
    [HttpPost]
    public async Task<IActionResult> GenerateDemo(CancellationToken ct)
    {
        await demo.CreateAsync(access.UserId, ct); TempData["Success"] = "DemoCreated"; return RedirectToAction(nameof(Demo));
    }
}
