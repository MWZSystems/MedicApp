using System.Security.Claims;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
using Trainify.Domain;
namespace MedicApp.Tests;

public sealed class SqlFixture : IAsyncLifetime
{
    public string Connection { get; } = $"Server=(localdb)\\MSSQLLocalDB;Database=MedicApp_Test_{Guid.NewGuid():N};Integrated Security=true;TrustServerCertificate=true";
    public int EmployeeId { get; private set; }
    public int OtherId { get; private set; }
    public int DoctorPersonId { get; private set; }
    public int DoctorId { get; private set; }
    public string Password { get; } = $"Test-only-{Guid.NewGuid():N}!";
    public DateTime Tomorrow { get; } = DateTime.UtcNow.Date.AddDays(2).AddHours(14);
    public ValueTask InitializeAsync() => new(InitializeCoreAsync());
    private async Task InitializeCoreAsync()
    {
        var target = new SqlConnectionStringBuilder(Connection);
        if (target.DataSource != "(localdb)\\MSSQLLocalDB" || !target.InitialCatalog.StartsWith("MedicApp_Test_", StringComparison.Ordinal)) { throw new InvalidOperationException("Unsafe test target"); }
        await using var shared = Shared();
        await shared.Database.EnsureCreatedAsync();
        var employee = new Person { EmployeeNumber = "TEST-E1", FirstName = "Empleado ficticio" };
        var other = new Person { EmployeeNumber = "TEST-E2", FirstName = "Otro ficticio" };
        var doctor = new Person { EmployeeNumber = "TEST-D1", FirstName = "Doctor ficticio" };
        shared.People.AddRange(employee, other, doctor);
        await shared.SaveChangesAsync();
        EmployeeId = employee.Id; OtherId = other.Id; DoctorPersonId = doctor.Id;
        foreach (var item in new[] { ("employee", EmployeeId), ("other", OtherId), ("doctor", DoctorPersonId) })
        {
            var user = new AppUser { Id = item.Item1, UserName = item.Item1, NormalizedUserName = item.Item1.ToUpperInvariant(), PersonId = item.Item2, MustChangePassword = false, SecurityStamp = Guid.NewGuid().ToString() };
            user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, Password);
            shared.Users.Add(user);
            shared.LoginIdentifiers.Add(new() { Key = item.Item1.ToUpperInvariant(), UserId = user.Id });
        }
        await shared.SaveChangesAsync();
        await using var db = Medical();
        await db.Database.MigrateAsync();
        var staff = new Doctor { PersonId = DoctorPersonId, Specialty = "Medicina general", ProfessionalLicense = "TEST-ONLY", UpdatedById = "doctor" };
        db.Doctors.Add(staff);
        await db.SaveChangesAsync();
        DoctorId = staff.Id;
        db.AccessProfiles.Add(new() { UserId = "doctor", UpdatedById = "doctor", ManageAccess = true, ManageDoctors = true, ReadIntegration = true });
        db.Windows.Add(new() { DoctorId = DoctorId, StartsAt = Tomorrow, EndsAt = Tomorrow.AddHours(8), SlotMinutes = 15, UpdatedById = "doctor" });
        await db.SaveChangesAsync();
    }
    public TrainifyDbContext Shared() => new(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(Connection).Options);
    public MedicDbContext Medical() => new(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(Connection, s => s.MigrationsHistoryTable("__EFMigrationsHistory", "medic")).Options);
    public static HttpContextAccessor Context(string user, int person) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user), new Claim("personId", person.ToString(System.Globalization.CultureInfo.InvariantCulture))], "Test")) } };
    public static MedicalClock Clock() => new(Options.Create(new MedicalOptions()), TimeProvider.System);
    public static MedicalService Service(MedicDbContext db, TrainifyDbContext shared, string user, int person) => new(db, shared, new MedicalAccess(db, Context(user, person)), Clock(), Options.Create(new MedicalOptions()));
    public async ValueTask DisposeAsync()
    {
        await using var db = Shared();
        // The immutable fixture connection was checked before creation; only this unique test DB is removed.
        await db.Database.EnsureDeletedAsync();
    }
}
[CollectionDefinition("Sql", DisableParallelization = true)]
public class SqlTestGroup : ICollectionFixture<SqlFixture>;
