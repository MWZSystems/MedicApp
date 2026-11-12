using System.Net;
using System.Security.Claims;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace MedicApp.Tests;

/// <summary>Tablero de Inicio: solo conteos, limitados a lo que cada persona puede consultar. Nunca datos clínicos.</summary>
[Collection("Sql")]
public class DashboardTests(SqlFixture fixture)
{
    private sealed class Accessor(string user, int person) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user), new("personId", person.ToString(System.Globalization.CultureInfo.InvariantCulture))], "Test")) };
    }
    private static readonly MedicalClock Clock = new(Options.Create(new MedicalOptions()), TimeProvider.System);
    private static Task<MedicalDashboard> Build(MedicApp.Data.MedicDbContext db, string user, int person) =>
        new MedicalDashboardService(db, new MedicalAccess(db, new Accessor(user, person)), Clock).BuildAsync(key => key, CancellationToken.None);

    [Fact]
    public async Task Empleado_solo_ve_lo_suyo()
    {
        await using var db = fixture.Medical();
        var board = await Build(db, "employee", fixture.EmployeeId);
        Assert.Equal(["PendingAppointments", "ActiveLeaves"], board.Mine.Select(x => x.Label));
        Assert.Empty(board.Doctor); Assert.Empty(board.Clinic);
        Assert.Null(board.DoctorWeek); Assert.Null(board.ClinicMonth); Assert.Null(board.ClinicWeeks); Assert.Null(board.LowStock);
    }

    [Fact]
    public async Task Medico_ve_sus_citas_y_la_administracion_los_totales_de_la_clinica()
    {
        await using var db = fixture.Medical();
        var window = await db.Windows.AsNoTracking().FirstAsync(x => x.DoctorId == fixture.DoctorId);
        // Otro médico, sin perfil de administración, con una cita hoy: no debe sumarse a las citas del primero.
        var second = new Doctor { PersonId = fixture.OtherId, Specialty = "Tablero", ProfessionalLicense = "TEST-TABLERO", UpdatedById = "doctor" };
        db.Doctors.Add(second); await db.SaveChangesAsync();
        var now = Clock.UtcNow;
        var otherWindow = new AvailabilityWindow { DoctorId = second.Id, StartsAt = now.AddMinutes(-30), EndsAt = now.AddHours(2), SlotMinutes = 15, UpdatedById = "doctor" };
        db.Windows.Add(otherWindow); await db.SaveChangesAsync();
        var mine = new Appointment { PersonId = fixture.EmployeeId, DoctorId = fixture.DoctorId, AvailabilityWindowId = window.Id, StartsAt = now.AddMinutes(1), EndsAt = now.AddMinutes(16), RequestKey = Guid.NewGuid(), UpdatedById = "doctor" };
        var theirs = new Appointment { PersonId = fixture.EmployeeId, DoctorId = second.Id, AvailabilityWindowId = otherWindow.Id, StartsAt = now.AddMinutes(1), EndsAt = now.AddMinutes(16), Status = AppointmentStatus.Attended, RequestKey = Guid.NewGuid(), UpdatedById = "doctor" };
        db.Appointments.AddRange(mine, theirs); await db.SaveChangesAsync();
        try
        {
            var doctor = await Build(db, "doctor", fixture.DoctorPersonId);
            var (weekStart, weekEnd, monthStart, monthEnd) = MedicalDashboardService.Ranges(Clock);
            Assert.Equal(await db.Appointments.CountAsync(x => x.Available && x.DoctorId == fixture.DoctorId && x.StartsAt >= weekStart && x.StartsAt < weekEnd), (int)doctor.DoctorWeek!.Items.Sum(x => x.Value));
            Assert.Equal(["DashToday", "DashWeek"], doctor.Doctor.Select(x => x.Label));
            // El perfil de administración ve toda la clínica: las citas de los dos médicos.
            Assert.Equal(["DashMonth", "ActiveLeaves", "DashDoctors"], doctor.Clinic.Select(x => x.Label));
            Assert.Equal(await db.Appointments.CountAsync(x => x.Available && x.StartsAt >= monthStart && x.StartsAt < monthEnd), (int)doctor.ClinicMonth!.Items.Sum(x => x.Value));
            Assert.True(doctor.ClinicMonth.Items.Sum(x => x.Value) > doctor.DoctorWeek.Items.Sum(x => x.Value) - 1);
            Assert.Equal(6, doctor.ClinicWeeks!.Points.Count);
            Assert.Equal(doctor.ClinicWeeks.Points[^1].Value, await db.Appointments.CountAsync(x => x.Available && x.StartsAt >= weekStart && x.StartsAt < weekEnd));

            // El segundo médico solo ve las suyas y ningún total de la clínica.
            var other = await Build(db, "other", fixture.OtherId);
            Assert.Equal(1, other.DoctorWeek!.Items.Sum(x => x.Value));
            Assert.Equal(1, other.DoctorWeek.Items.Single(x => x.Label == "Attended").Value);
            Assert.Empty(other.Clinic); Assert.Null(other.ClinicMonth); Assert.Null(other.ClinicWeeks);
        }
        finally
        {
            db.Appointments.RemoveRange(mine, theirs); db.Windows.Remove(otherWindow); db.Doctors.Remove(second);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Inventario_bajo_minimo_cuenta_solo_articulos_disponibles()
    {
        await using var db = fixture.Medical();
        var before = int.Parse((await Build(db, "doctor", fixture.DoctorPersonId)).LowStock!.Value, System.Globalization.CultureInfo.InvariantCulture);
        var low = new StockItem { Code = "T-BAJO", Name = "Bajo mínimo", Unit = "pza", MinimumStock = 5, CurrentStock = 1, UpdatedById = "doctor" };
        var hidden = new StockItem { Code = "T-OCULTO", Name = "No disponible", Unit = "pza", MinimumStock = 5, CurrentStock = 1, Available = false, UpdatedById = "doctor" };
        var enough = new StockItem { Code = "T-BIEN", Name = "Suficiente", Unit = "pza", MinimumStock = 5, CurrentStock = 5, UpdatedById = "doctor" };
        db.StockItems.AddRange(low, hidden, enough); await db.SaveChangesAsync();
        try
        {
            var stat = (await Build(db, "doctor", fixture.DoctorPersonId)).LowStock!;
            Assert.Equal(before + 1, int.Parse(stat.Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(MWZSystem.Ui.MwzTone.Aviso, stat.Tone);
        }
        finally { db.StockItems.RemoveRange(low, hidden, enough); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task Inicio_muestra_el_tablero_segun_la_persona()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(builder, "Testing");
            builder.ConfigureAppConfiguration((_, config) => Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(config, new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", "TestHost", Guid.NewGuid().ToString("N")), ["AllowedHosts"] = "localhost" }));
        });
        async Task<string> Home(string name)
        {
            using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var token = System.Text.RegularExpressions.Regex.Match(await client.GetStringAsync("/Account/Login"), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = name, ["Password"] = fixture.Password, ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            return WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        }
        var employee = await Home("employee");
        Assert.Contains("mwz-tablero", employee); Assert.Contains("Citas pendientes", employee); Assert.Contains("Tu salud", employee);
        Assert.DoesNotContain("Citas del mes", employee); Assert.DoesNotContain("Artículos bajo el mínimo", employee);
        var doctor = await Home("doctor");
        Assert.Contains("Citas del mes", doctor); Assert.Contains("Citas de hoy", doctor); Assert.Contains("Artículos bajo el mínimo", doctor);
    }
}
