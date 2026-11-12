using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
namespace MedicApp.Controllers;

public class HomeController(MedicalDashboardService dashboard, IStringLocalizer<UiText> text, ILogger<HomeController> log) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Si el tablero falla, Inicio se muestra sin él.
        try { return View(await dashboard.BuildAsync(key => text[key], ct)); }
        catch (Exception error) when (error is not OperationCanceledException and not MedicalRuleException) { log.DashboardFailed(error); return View((MedicalDashboard?)null); }
    }
    [AllowAnonymous] public IActionResult Error() { Response.StatusCode = 500; this.Error("UnexpectedError"); return View(); }
}
