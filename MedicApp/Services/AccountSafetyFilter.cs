using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace MedicApp.Services;

public class AccountSafetyFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true && context.HttpContext.User.FindFirst("mustChangePassword")?.Value == "true" && context.RouteData.Values["controller"]?.ToString() != "Account")
        { context.Result = new RedirectToActionResult("ChangePassword", "Account", null); }
    }
    public void OnActionExecuted(ActionExecutedContext context) { }
}
