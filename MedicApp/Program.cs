using MedicApp.Data;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Trainify.Data;
using Trainify.Domain;
using Trainify.Services;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--grant-admin").ToArray());
builder.Configuration.AddUserSecrets<Program>(optional: true).AddEnvironmentVariables();
var connection = builder.Configuration.GetConnectionString("Workforce");
var dataDirectory = Path.GetFullPath(builder.Configuration["Medical:DataDirectory"] ?? "App_Data", builder.Environment.ContentRootPath);
if (dataDirectory.StartsWith(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), StringComparison.OrdinalIgnoreCase))
{ throw new InvalidOperationException("DataDirectory must be outside wwwroot."); }
builder.Services.AddOptions<MedicalOptions>().BindConfiguration("Medical").ValidateDataAnnotations().Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.TimeZoneId, out _), "Invalid timezone").ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MedicalClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<TrainifyDbContext>((services, o) => o.UseSqlServer(DatabaseConfiguration.Resolve(services.GetRequiredService<IConfiguration>())));
builder.Services.AddDbContext<MedicDbContext>((services, o) => o.UseSqlServer(DatabaseConfiguration.Resolve(services.GetRequiredService<IConfiguration>()), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "medic")));
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<TrainifyDbContext>().AddDefaultTokenProviders().AddClaimsPrincipalFactory<TrainifyClaimsFactory>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "MedicApp.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.EventsType = typeof(SessionCookieEvents);
});
builder.Services.AddAntiforgery(o => { o.Cookie.Name = "MedicApp.Antiforgery"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; });
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "Keys"))).SetApplicationName("MedicApp");
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<MedicalAccess>();
builder.Services.AddScoped<MedicalService>();
builder.Services.AddScoped<ListReader>();
builder.Services.AddScoped<CalendarReader>();
builder.Services.AddScoped<IAuthorizationHandler, MedicalAuthorizationHandler>();
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var key in (string[])["Doctor", "Doctors", "Access", "Integration"])
    { o.AddPolicy(key, p => p.RequireAuthenticatedUser().AddRequirements(new MedicalRequirement(key))); }
    o.AddPolicy("ClinicalOrAdmin", p => p.RequireAuthenticatedUser().RequireAssertion(async context =>
    {
        var http = context.Resource as HttpContext;
        if (http == null) { return false; }
        var access = http.RequestServices.GetRequiredService<MedicalAccess>();
        return await access.DoctorIdAsync(http.RequestAborted) != null || await access.CanManageDoctorsAsync(http.RequestAborted);
    }));
});
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(o => o.SetDefaultCulture("es-MX").AddSupportedCultures("es-MX").AddSupportedUICultures("es-MX"));
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<AccountSafetyFilter>();
}).AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(MedicApp.UiText)));
builder.Services.AddExceptionHandler<MedicalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddSerilog((services, configuration) => configuration.MinimumLevel.Information().MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning).Enrich.FromLogContext().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).WriteTo.File(Path.Combine(dataDirectory, "Logs", "medic-.log"), rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10485760, rollOnFileSizeLimit: true, retainedFileCountLimit: 14, formatProvider: System.Globalization.CultureInfo.InvariantCulture));
var app = builder.Build();
if (args.Contains("--grant-admin"))
{
    if (string.IsNullOrWhiteSpace(connection)) { throw new InvalidOperationException("Configure ConnectionStrings:Workforce."); }
    var userName = builder.Configuration["Bootstrap:UserName"] ?? throw new InvalidOperationException("Configure Bootstrap:UserName.");
    using var scope = app.Services.CreateScope();
    var shared = scope.ServiceProvider.GetRequiredService<TrainifyDbContext>();
    var medical = scope.ServiceProvider.GetRequiredService<MedicDbContext>();
    var normalized = userName.ToUpperInvariant();
    var user = await shared.Users.AsNoTracking().SingleAsync(x => x.NormalizedUserName == normalized && x.Active && x.Person.Active);
    if (user.MustChangePassword) { throw new InvalidOperationException("Complete the password change from WFMCore first."); }
    await using var tx = await medical.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    if (await medical.AccessProfiles.AnyAsync(x => x.Available && x.ManageAccess)) { throw new InvalidOperationException("Use the access administration screen after bootstrap."); }
    var profile = await medical.AccessProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id);
    if (profile == null) { profile = new() { UserId = user.Id }; medical.AccessProfiles.Add(profile); }
    profile.ManageDoctors = true; profile.ManageAccess = true; profile.Available = true;
    profile.UpdatedById = user.Id; profile.LastUpdated = DateTime.UtcNow;
    await medical.SaveChangesAsync();
    medical.Audit.Add(new() { EntityType = "AccessProfile", EntityId = profile.Id, Operation = "Bootstrap", UpdatedById = user.Id });
    await medical.SaveChangesAsync();
    await tx.CommitAsync();
    return;
}
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.CacheControl = "no-store";
    using (Serilog.Context.LogContext.PushProperty("TraceId", context.TraceIdentifier)) { await next(); }
});
app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
public partial class Program;
