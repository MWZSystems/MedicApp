using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Workforce.Shared;
namespace MedicApp.Tests;

public class PagingTests
{
    private sealed class TestController : Controller;
    internal static Controller Controller(string query) => new TestController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Request = { QueryString = new QueryString(query) } } } };
    [Theory]
    [InlineData("?pagina=2", 2, 25, 26, 50)]
    [InlineData("?pagina=99", 3, 25, 51, 63)]
    [InlineData("?pagina=abc&tamano=10", 1, 25, 1, 25)]
    [InlineData("?pagina=-2", 1, 25, 1, 25)]
    [InlineData("?tamano=50", 1, 50, 1, 50)]
    public void ClampsAndReportsCorrectTotals(string query, int number, int size, int from, int to)
    {
        var controller = Controller(query); var rows = Enumerable.Range(1, 63).ToPage(controller); var info = Assert.IsType<PageInfo>(controller.ViewData[Paging.ViewDataKey()]);
        Assert.Equal(number, info.Number); Assert.Equal(size, info.Size); Assert.Equal(63, info.Total); Assert.Equal(from, info.From); Assert.Equal(to, info.To); Assert.Equal(to - from + 1, rows.Count); Assert.Equal(from, rows[0]);
    }
    [Fact]
    public void IndependentListsAndSearchArePrefixed()
    {
        var controller = Controller("?visitspagina=2&visitstamano=50&filespagina=3&filestamano=25&visitsbuscar=%20consulta%20&filesbuscar=%20%20");
        Enumerable.Range(1, 120).ToPage(controller, "visits"); Enumerable.Range(1, 80).ToPage(controller.Request, controller.ViewData, "files");
        var visits = Assert.IsType<PageInfo>(controller.ViewData[Paging.ViewDataKey("visits")]); var files = Assert.IsType<PageInfo>(controller.ViewData[Paging.ViewDataKey("files")]);
        Assert.Equal(2, visits.Number); Assert.Equal(50, visits.Size); Assert.Equal(3, files.Number); Assert.Equal(25, files.Size); Assert.Equal("visitspagina", visits.PageKey); Assert.Equal("filestamano", files.SizeKey); Assert.Equal("consulta", controller.Search("visits")); Assert.Null(controller.Search("files"));
        controller.Request.QueryString = new QueryString("?buscar=" + new string('x', 150)); Assert.Equal(100, controller.Search()!.Length);
    }
    [Fact]
    public void EmptyListHasZeroBounds()
    {
        var controller = Controller("?pagina=20"); Assert.Empty(Array.Empty<int>().ToPage(controller)); var info = Assert.IsType<PageInfo>(controller.ViewData[Paging.ViewDataKey()]); Assert.Equal(1, info.Number); Assert.Equal(1, info.Pages); Assert.Equal(0, info.From); Assert.Equal(0, info.To);
    }
}

[CollectionDefinition("PagingSql", DisableParallelization = true)]
public class PagingGroup : ICollectionFixture<SqlFixture>;
[Collection("PagingSql")]
public class PagingSqlTests(SqlFixture fixture) : IAsyncDisposable
{
    private readonly string data = Path.Combine(Path.GetTempPath(), "MedicAppPagingTests", Guid.NewGuid().ToString("N"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Capture : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default) { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
    public async ValueTask DisposeAsync()
    {
        var target = Path.GetFullPath(data); var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MedicAppPagingTests"));
        if (Path.GetDirectoryName(target) != parent) { throw new InvalidOperationException("Unsafe test cleanup"); }
        if (Directory.Exists(target)) { Directory.Delete(target, true); }
        await ValueTask.CompletedTask; GC.SuppressFinalize(this);
    }
    private async Task SeedAsync()
    {
        await using var db = fixture.Medical(); if (await db.StockItems.AnyAsync(x => x.Code == "PAGING-000", Ct)) { return; }
        for (var i = 0; i < 70; i++) { db.StockItems.Add(new() { Code = "PAGING-" + i.ToString("000", CultureInfo.InvariantCulture), Name = i < 60 ? "Insumo coincidencia" : "Otro producto", Unit = "pieza", Lot = "LOTE-PRUEBA", CurrentStock = i < 30 ? 2 : 100, MinimumStock = 5, UpdatedById = "doctor" }); }
        await db.SaveChangesAsync(Ct);
    }
    [Fact]
    public async Task FilteredDatabaseQueryFetchesOnlyRequestedPageAndCountsSameScope()
    {
        await SeedAsync(); var capture = new Capture(); await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(fixture.Connection).AddInterceptors(capture).Options);
        var controller = PagingTests.Controller("?buscar=coincidencia&pagina=2&tamano=25"); var search = controller.Search()!;
        var rows = await db.StockItems.AsNoTracking().Where(x => x.Name.Contains(search)).OrderBy(x => x.Name).ThenBy(x => x.Id).ToPageAsync(controller);
        Assert.Equal(25, rows.Count); var info = Assert.IsType<PageInfo>(controller.ViewData[Paging.ViewDataKey()]); Assert.Equal(60, info.Total); Assert.Equal(2, info.Number);
        Assert.Contains(capture.Sql, x => x.Contains("COUNT(*)", StringComparison.Ordinal) && x.Contains("WHERE", StringComparison.Ordinal)); Assert.Contains(capture.Sql, x => x.Contains("OFFSET", StringComparison.Ordinal) && x.Contains("FETCH NEXT", StringComparison.Ordinal));
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = data, ["AllowedHosts"] = "localhost" })); });
    private async Task LoginAsync(HttpClient client)
    {
        var login = await client.GetStringAsync("/Account/Login"); var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = "doctor", ["Password"] = fixture.Password, ["__RequestVerificationToken"] = token })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    [Fact]
    public async Task InventoryPageSearchFiltersAndSizePreserveUrlScope()
    {
        await SeedAsync(); await using var factory = Factory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); await LoginAsync(client);
        var html = await client.GetStringAsync("/Inventory?buscar=coincidencia&pagina=2&tamano=25"); Assert.Contains("Mostrando 26 a 50 de 60", html); Assert.Contains("pagina=3", html); Assert.Contains("buscar=coincidencia", html); Assert.DoesNotContain("data-local-table", html);
        var filtered = await client.GetStringAsync("/Inventory?buscar=coincidencia&stock=low&pagina=2&tamano=25"); Assert.Contains("Mostrando 26 a 30 de 30", filtered); Assert.Contains("stock=low", filtered);
        var empty = await client.GetStringAsync("/Inventory?buscar=no-coincide"); Assert.Contains("Ninguno coincide", empty); Assert.DoesNotContain("list-pager", empty);
        var sized = await client.GetStringAsync("/Inventory?buscar=coincidencia&tamano=100"); Assert.Contains("Mostrando 1 a 60 de 60", sized);
    }
    [Theory]
    [InlineData("https://localhost", true)]
    [InlineData("https://another.invalid", false)]
    public async Task InventoryMovementReturnsOnlyToSameSiteItemWithFilters(string origin, bool preserves)
    {
        await SeedAsync(); await using var db = fixture.Medical(); var item = await db.StockItems.FirstAsync(x => x.Code == "PAGING-000", Ct);
        await using var factory = Factory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); await LoginAsync(client);
        var path = "/Inventory/Item/" + item.Id.ToString(CultureInfo.InvariantCulture); var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Inventory/Move"); request.Headers.Referrer = new Uri(origin + path + "?pagina=2&buscar=DEMO&kind=0");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["StockItemId"] = item.Id.ToString(CultureInfo.InvariantCulture), ["Kind"] = "0", ["Quantity"] = "1", ["Reason"] = "DEMO redirect test", ["RequestKey"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = token });
        using var response = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(preserves ? path + "?pagina=2&buscar=DEMO&kind=0" : path, response.Headers.Location!.ToString());
    }
    [Fact]
    public async Task InactiveInventoryRemainsReadableWithoutMovementForm()
    {
        await using var db = fixture.Medical(); var item = new StockItem { Code = "PAGING-INACTIVE", Name = "Insumo inactivo", Unit = "pieza", Available = false, UpdatedById = "doctor" }; db.StockItems.Add(item); await db.SaveChangesAsync(Ct);
        await using var factory = Factory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); await LoginAsync(client);
        var list = await client.GetStringAsync("/Inventory?active=false&buscar=PAGING-INACTIVE"); Assert.Contains("PAGING-INACTIVE", list);
        var detail = await client.GetStringAsync("/Inventory/Item/" + item.Id.ToString(CultureInfo.InvariantCulture)); Assert.Contains("Insumo inactivo", detail); Assert.DoesNotContain("/Inventory/Move", detail); Assert.Contains("Historial", detail);
    }
    [Fact]
    public async Task ListsRenderWithDatabaseSearchesAndIndependentClinicalPrefixes()
    {
        await using var db = fixture.Medical(); var leave = new MedicalLeave { PersonId = fixture.EmployeeId, DoctorId = fixture.DoctorId, StartsAt = fixture.Tomorrow, EndsAt = fixture.Tomorrow.AddDays(1), Reference = "Paging evidence", UpdatedById = "doctor" }; db.Leaves.Add(leave); await db.SaveChangesAsync(Ct);
        await using var factory = Factory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); await LoginAsync(client);
        var person = fixture.EmployeeId.ToString(CultureInfo.InvariantCulture);
        foreach (var path in new[] { "/Admin/Doctors?buscar=ficticio&active=true", "/Admin/Access?buscar=doctor&permission=access", "/Admin/Audit?buscar=doctor", "/Appointments/List?buscar=ficticio", "/Leaves?buscar=ficticio", "/Agenda?buscar=15&period=all", "/LeaveDocuments/Record?personId=" + person + "&buscar=Paging", "/LeaveDocuments?leaveId=" + leave.Id.ToString(CultureInfo.InvariantCulture) + "&buscar=pdf", "/ClinicalRecords?personId=" + person + "&visitsbuscar=consulta&filesbuscar=pdf&visitspagina=2&filespagina=3" })
        {
            using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var html = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("data-local-table", html);
            if (path.StartsWith("/ClinicalRecords", StringComparison.Ordinal)) { Assert.Contains("name=\"visitsbuscar\"", html); Assert.Contains("name=\"filesbuscar\"", html); Assert.Contains("filespagina", html); }
        }
    }
}
