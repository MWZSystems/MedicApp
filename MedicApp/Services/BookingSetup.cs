using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Trainify.Data;
using Trainify.Domain;
namespace MedicApp.Services;

public class BookingSetup(IConfiguration configuration, MedicalClock clock)
{
    private static readonly int[] SampleHours = [8, 10, 14];
    public const string DemoLicense = "MEDIC-DEMO-BOOKING";
    public async Task<string> PrepareAsync(string actorId, string? calendarAdmin, CancellationToken ct)
    {
        await using var connection = new SqlConnection(DatabaseConfiguration.Resolve(configuration));
        await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(connection).Options);
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await shared.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.Appointments", ct);
        if (!await db.AccessProfiles.AnyAsync(x => x.UserId == actorId && x.Available && x.ManageAccess, ct)) { throw new MedicalRuleException("Forbidden"); }
        if (!string.IsNullOrWhiteSpace(calendarAdmin))
        {
            var normalized = calendarAdmin.ToUpperInvariant();
            var target = await shared.Users.Where(x => x.NormalizedUserName == normalized && x.Active && x.Person.Active).Select(x => x.Id).SingleAsync(ct);
            var profile = await db.AccessProfiles.SingleOrDefaultAsync(x => x.UserId == target, ct);
            if (profile == null) { profile = new AccessProfile { UserId = target }; db.AccessProfiles.Add(profile); }
            if (!profile.Available || !profile.ManageAccess || !profile.ManageDoctors)
            {
                profile.Available = true; profile.ManageAccess = true; profile.ManageDoctors = true; profile.UpdatedById = actorId; profile.LastUpdated = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                db.Audit.Add(new AuditEntry { EntityType = "AccessProfile", EntityId = profile.Id, Operation = "EnableCalendarAdministration", UpdatedById = actorId, LastUpdated = clock.UtcNow });
            }
        }
        var doctor = await db.Doctors.SingleOrDefaultAsync(x => x.ProfessionalLicense == DemoLicense, ct);
        var createdPeople = new List<int>();
        async Task<Person> PersonAsync(string number, string name)
        {
            var row = await shared.People.SingleOrDefaultAsync(x => x.EmployeeNumber == number, ct);
            if (row != null) { if (!row.Active || !row.FirstName.StartsWith("DEMO", StringComparison.Ordinal)) { throw new MedicalRuleException("Concurrency"); } return row; }
            row = new Person { EmployeeNumber = number, FirstName = name };
            shared.People.Add(row); await shared.SaveChangesAsync(ct); createdPeople.Add(row.Id); return row;
        }
        var staff = await PersonAsync(DemoDataService.Prefix + "CONSULTORIO", "DEMO Consultorio de pruebas");
        if (doctor == null)
        {
            doctor = new Doctor { PersonId = staff.Id, ProfessionalLicense = DemoLicense, Specialty = "DEMO Medicina general", UseRecurringSchedule = true, UpdatedById = actorId, LastUpdated = clock.UtcNow };
            db.Doctors.Add(doctor); await db.SaveChangesAsync(ct);
        }
        if (doctor.PersonId != staff.Id || !doctor.Available || !doctor.UseRecurringSchedule) { throw new MedicalRuleException("Concurrency"); }
        var employees = new List<Person>();
        for (var index = 1; index <= 3; index++) { employees.Add(await PersonAsync(DemoDataService.Prefix + "E" + index.ToString(CultureInfo.InvariantCulture), "DEMO Empleado " + index.ToString(CultureInfo.InvariantCulture))); }
        var createdAppointments = new List<int>();
        var createdWindows = new List<int>();
        var planner = new AvailabilityPlanner(db, shared, clock, Options.Create(new MedicalOptions { DemoBookingEnabled = true }));
        var today = DateOnly.FromDateTime(clock.Local(clock.UtcNow));
        for (var offset = 0; offset < 7; offset++)
        {
            var day = today.AddDays(offset);
            var spec = new BookingWindow(0, doctor.Id, staff.Id, clock.ToUtc(day.ToDateTime(TimeOnly.MinValue)), clock.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue)), 30);
            var existingWindow = await db.Windows.AnyAsync(x => x.Generated && x.DoctorId == doctor.Id && x.StartsAt == spec.StartsAt && x.SlotMinutes == 30, ct);
            var window = await planner.MaterializeAsync(spec, actorId, ct);
            if (!existingWindow) { createdWindows.Add(window); }
            for (var index = 0; index < 3; index++)
            {
                var local = day.ToDateTime(new TimeOnly(SampleHours[index], 0));
                var start = clock.ToUtc(local); var end = start.AddMinutes(30);
                var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("MedicApp.BookingDemo." + local.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture))).AsSpan(0, 16));
                if (await db.Appointments.AnyAsync(x => x.RequestKey == key || (x.DoctorId == doctor.Id && x.Available && x.Status == AppointmentStatus.Scheduled && x.StartsAt < end && x.EndsAt > start), ct)) { continue; }
                var appointment = new Appointment { PersonId = employees[index].Id, DoctorId = doctor.Id, AvailabilityWindowId = window, StartsAt = start, EndsAt = end, RequestKey = key, Status = start <= clock.UtcNow ? AppointmentStatus.Attended : AppointmentStatus.Scheduled, UpdatedById = actorId, LastUpdated = clock.UtcNow };
                db.Appointments.Add(appointment); await db.SaveChangesAsync(ct); createdAppointments.Add(appointment.Id);
            }
        }
        foreach (var inventory in new[] { ("People", createdPeople), ("Windows", createdWindows), ("Appointments", createdAppointments) })
        {
            if (inventory.Item2.Count > 0) { db.Audit.Add(new AuditEntry { EntityType = "BookingDemoBatch", EntityId = doctor.Id, Operation = "Create" + inventory.Item1, UpdatedById = actorId, LastUpdated = clock.UtcNow, Reason = JsonSerializer.Serialize(new { Marker = DemoLicense, Ids = inventory.Item2 }) }); }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return $"Agenda DEMO recurrente habilitada. Citas nuevas: {createdAppointments.Count}; personas nuevas: {createdPeople.Count}. Bloques de 30 minutos; reservas por 7 días. Sin cuentas ni eventos laborales.";
    }
}
