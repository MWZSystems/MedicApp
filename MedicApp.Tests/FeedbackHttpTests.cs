using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
namespace MedicApp.Tests;

[Collection("Sql")]
public partial class FeedbackHttpTests(SqlFixture fixture)
{
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureServices(services => services.AddControllersWithViews().AddApplicationPart(typeof(FeedbackProbeController).Assembly)); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = Path.Combine(Path.GetTempPath(), "MedicAppFeedbackTests"), ["AllowedHosts"] = "localhost" })); });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex Token();
    private static string Value(string page) => WebUtility.HtmlDecode(Token().Match(page).Groups[1].Value);
    private async Task Login(HttpClient client)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = "employee", ["Password"] = fixture.Password, ["__RequestVerificationToken"] = Value(page) })); Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
    [Fact]
    public async Task LoginAndLogoutNoticesSurviveRedirectAndAreNotRepeated()
    {
        await using var factory = Factory(); using var client = Client(factory); await Login(client);
        var home = await client.GetStringAsync("/"); /* Los avisos viajan como lista para MWZSystem.Ui; la duración de cada tipo la decide el paquete. */ Assert.Contains("&quot;tipo&quot;:&quot;success&quot;", home); Assert.Contains("v1.0.0", home); Assert.DoesNotContain("alert alert-success", home);
        var next = await client.GetStringAsync("/"); Assert.DoesNotContain("&quot;tipo&quot;:&quot;success&quot;", next);
        using var result = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Value(home) })); Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var login = await client.GetStringAsync("/Account/Login"); Assert.Contains("&quot;tipo&quot;:&quot;success&quot;", login); Assert.Contains("class=\"mwz-toasts\"", login);
    }
    [Fact]
    public async Task InvalidFieldAndGeneralErrorsBecomePersistentErrorToasts()
    {
        await using var factory = Factory(); using var client = Client(factory); var login = await client.GetStringAsync("/Account/Login");
        using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = "", ["Password"] = "", ["__RequestVerificationToken"] = Value(login) }));
        var page = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.Contains("&quot;tipo&quot;:&quot;error&quot;", page); Assert.Contains("Revisa los datos marcados", page); Assert.Contains("field-validation-error", page);
        await Login(client); var home = await client.GetStringAsync("/");
        using var password = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string> { ["Current"] = "not-the-current-password", ["Password"] = "Valid-Test-only-123456!", ["Confirm"] = "Valid-Test-only-123456!", ["__RequestVerificationToken"] = Value(home) }));
        var invalid = await password.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.Contains("&quot;tipo&quot;:&quot;error&quot;", invalid); Assert.DoesNotContain("not-the-current-password", invalid);
    }
    [Fact]
    public async Task HtmlRuleErrorsUseSharedLayoutAndJsonStillReturnsProblemDetails()
    {
        await using var factory = Factory(); using var client = Client(factory); await Login(client); var home = await client.GetStringAsync("/");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var html = await client.PostAsync("/Appointments/Reserve", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Value(home) }));
        var page = await html.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.True(html.StatusCode == HttpStatusCode.UnprocessableEntity, string.Join(";", Regex.Matches(page, "(?s)<h[12][^>]*>.*?</h[12]>").Select(x => x.Value))); Assert.Contains("&quot;tipo&quot;:&quot;error&quot;", page); Assert.Contains("app-version-bar", page); Assert.DoesNotContain("InvalidReservation", page);
        client.DefaultRequestHeaders.Accept.Clear(); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var json = await client.PostAsync("/Appointments/Reserve", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Value(home) })); Assert.Equal(HttpStatusCode.UnprocessableEntity, json.StatusCode); Assert.Contains("json", json.Content.Headers.ContentType!.MediaType!); Assert.DoesNotContain("mwz-toasts", await json.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task InvalidHtmlPostPreservesBadRequestStatusAndDisplaysFriendlyToast()
    {
        await using var factory = Factory(); using var client = Client(factory); await Login(client); var home = await client.GetStringAsync("/"); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var result = await client.PostAsync("/Appointments/Change", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Value(home) })); Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode); Assert.Contains("&quot;tipo&quot;:&quot;error&quot;", await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task UnexpectedErrorsDoNotExposeTechnicalDetailsAndToastTextIsEncoded()
    {
        await using var factory = Factory(); using var client = Client(factory); client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var error = await client.GetAsync("/__feedback-test/unexpected"); Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        var page = await error.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.Contains("&quot;tipo&quot;:&quot;error&quot;", page); Assert.DoesNotContain("PRIVATE-TEST-DETAIL", page); Assert.DoesNotContain("InvalidOperationException", page);
        var encoded = await client.GetStringAsync("/__feedback-test/encoded"); /* El texto viaja en la lista de avisos, escapado por System.Text.Json; mwz-ui.js lo escribe como texto. */ Assert.Contains("\\u003Cscript\\u003E", encoded); Assert.DoesNotContain("<script>bad()</script>", encoded);
    }

}

// Discovered only by this test factory; this assembly is not included in the production application.
public class FeedbackProbeController : Controller
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous, HttpGet("/__feedback-test/unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException("PRIVATE-TEST-DETAIL: " + HttpContext.TraceIdentifier);
    [Microsoft.AspNetCore.Authorization.AllowAnonymous, HttpGet("/__feedback-test/encoded")]
    public IActionResult Encoded() { this.Success("<script>bad()</script>"); ViewBag.Appointments = 0; ViewBag.Leaves = 0; return View("~/Views/Home/Index.cshtml"); }
}
