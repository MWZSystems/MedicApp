using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Trainify.Data;
using Trainify.Domain;
using Trainify.Services;
namespace MedicApp.Controllers;

public class AccountController(IdentityService ids, SignInManager<AppUser> signIn, UserManager<AppUser> users, TrainifyDbContext shared, IStringLocalizer<UiText> text, ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous, HttpGet] public IActionResult Login(string? returnUrl) => View(new LoginForm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return View(input); }
        var user = await ids.FindAsync(input.Identifier);
        if (user is { Active: true } && await shared.People.AsNoTracking().AnyAsync(x => x.Id == user.PersonId && x.Active, ct))
        {
            var result = await signIn.PasswordSignInAsync(user, input.Password, false, true);
            if (result.Succeeded)
            {
                if (user.MustChangePassword) { return RedirectToAction(nameof(ChangePassword)); }
                return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl) : RedirectToAction("Index", "Home");
            }
        }
        MedicalLog.LoginRejected(logger, HttpContext.TraceIdentifier);
        ModelState.AddModelError("", text["InvalidLogin"]);
        ModelState.Remove(nameof(input.Password));
        input.Password = "";
        return View(input);
    }
    [HttpPost] public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction(nameof(Login)); }
    [HttpGet] public IActionResult ChangePassword() => View(new PasswordForm());
    [HttpPost]
    public async Task<IActionResult> ChangePassword(PasswordForm input)
    {
        if (!ModelState.IsValid) { return View(input); }
        var user = await users.GetUserAsync(User);
        if (user == null) { return Challenge(); }
        var result = await users.ChangePasswordAsync(user, input.Current, input.Password);
        if (!result.Succeeded) { ModelState.AddModelError("", text["PasswordRejected"]); return View(input); }
        user.MustChangePassword = false;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) { ModelState.AddModelError("", text["PasswordRejected"]); return View(input); }
        await signIn.RefreshSignInAsync(user);
        return RedirectToAction("Index", "Home");
    }
    [AllowAnonymous, HttpGet] public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}
