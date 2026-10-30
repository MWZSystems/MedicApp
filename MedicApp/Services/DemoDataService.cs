using System.Data;
using System.Globalization;
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

public sealed class DemoDataService(IConfiguration configuration, MedicalClock clock, IOptions<MedicalOptions> options)
{
    public const string Prefix = "MEDIC-DEMO-";
    private const string License = "MEDIC-DEMO-DOCTOR-001";
    public async Task<string> CreateAsync(string actorId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(DatabaseConfiguration.Resolve(configuration));
        await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(connection).Options);
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await shared.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.DemoData", ct);
        if (!await db.AccessProfiles.AnyAsync(x => x.UserId == actorId && x.Available && x.ManageAccess, ct)) { throw new MedicalRuleException("Forbidden"); }
        var existing = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(x => x.ProfessionalLicense == License, ct);
        if (existing != null)
        {
            if (!await shared.People.AnyAsync(x => x.Id == existing.PersonId && x.EmployeeNumber == Prefix + "D1", ct)) { throw new MedicalRuleException("Concurrency"); }
            return "Ya existe el lote MEDIC-DEMO; no se duplicó.";
        }
        if (await shared.People.AnyAsync(x => x.EmployeeNumber.StartsWith(Prefix), ct)) { throw new MedicalRuleException("Concurrency"); }
        var people = new[] {
            new Person { EmployeeNumber = Prefix + "D1", FirstName = "DEMO Doctor General" },
            new Person { EmployeeNumber = Prefix + "E1", FirstName = "DEMO Empleado Uno" },
            new Person { EmployeeNumber = Prefix + "E2", FirstName = "DEMO Empleado Dos" },
            new Person { EmployeeNumber = Prefix + "E3", FirstName = "DEMO Empleado Tres" }
        };
        shared.People.AddRange(people);
        await shared.SaveChangesAsync(ct);
        var doctor = new Doctor { PersonId = people[0].Id, Specialty = "DEMO Medicina general", ProfessionalLicense = License, UpdatedById = actorId, LastUpdated = clock.UtcNow };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        var first = clock.Local(clock.UtcNow).Date.AddDays(-3);
        var windows = new List<AvailabilityWindow>();
        for (var day = 0; day < 11; day++)
        {
            var start = clock.ToUtc(first.AddDays(day));
            var end = clock.ToUtc(first.AddDays(day + 1));
            windows.Add(new AvailabilityWindow { DoctorId = doctor.Id, StartsAt = start, EndsAt = end, SlotMinutes = options.Value.DefaultSlotMinutes, UpdatedById = actorId, LastUpdated = clock.UtcNow });
        }
        db.Windows.AddRange(windows);
        await db.SaveChangesAsync(ct);
        var appointments = new List<Appointment>();
        foreach (var window in windows)
        {
            var step = TimeSpan.FromMinutes(window.SlotMinutes);
            var slots = new List<DateTime>();
            for (var at = window.StartsAt; at.Add(step) <= window.EndsAt; at = at.Add(step))
            {
                if (options.Value.Allows(clock.Local(at), clock.Local(at.Add(step)))) { slots.Add(at); }
            }
            var selected = slots.Where((_, index) => index % Math.Max(1, slots.Count / 3) == 0).Take(3).ToArray();
            for (var index = 0; index < selected.Length; index++)
            {
                var past = selected[index] < clock.UtcNow;
                var status = past ? (index == 1 ? AppointmentStatus.NoShow : AppointmentStatus.Attended) : (index == 2 ? AppointmentStatus.Cancelled : AppointmentStatus.Scheduled);
                appointments.Add(new Appointment { PersonId = people[index + 1].Id, DoctorId = doctor.Id, AvailabilityWindowId = window.Id, StartsAt = selected[index], EndsAt = selected[index].Add(step), Status = status, CancellationReason = status == AppointmentStatus.Cancelled ? "DEMO Cancelación de ejemplo" : null, RequestKey = Guid.NewGuid(), UpdatedById = actorId, LastUpdated = clock.UtcNow });
            }
        }
        db.Appointments.AddRange(appointments);
        await db.SaveChangesAsync(ct);
        // This inventory identifies the exact generated records for a later reviewed cleanup.
        foreach (var inventory in new[] { ("People", people.Select(x => x.Id)), ("Doctor", new[] { doctor.Id }.AsEnumerable()), ("Windows", windows.Select(x => x.Id)), ("Appointments", appointments.Select(x => x.Id)) })
        {
            db.Audit.Add(new AuditEntry
            {
                EntityType = "DemoBatch",
                EntityId = doctor.Id,
                Operation = "Create" + inventory.Item1,
                UpdatedById = actorId,
                LastUpdated = clock.UtcNow,
                Reason = JsonSerializer.Serialize(new { Marker = Prefix, Ids = inventory.Item2 })
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"4 personas DEMO, 1 doctor DEMO, {windows.Count} disponibilidades y {appointments.Count} citas. Sin cuentas ni eventos laborales.";
    }
    public async Task<IReadOnlyList<object>> EventsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        CalendarReader.ValidateRange(from, to);
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(DatabaseConfiguration.Resolve(configuration)).Options);
        await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(DatabaseConfiguration.Resolve(configuration)).Options);
        var people = await shared.People.AsNoTracking().Where(x => x.EmployeeNumber.StartsWith(Prefix)).ToDictionaryAsync(x => x.Id, x => x.FirstName, ct);
        var ids = people.Keys.ToArray();
        var doctors = await db.Doctors.AsNoTracking().Where(x => ids.Contains(x.PersonId) && x.ProfessionalLicense == License).Select(x => x.Id).ToListAsync(ct);
        var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = clock.ToUtc(to.ToDateTime(TimeOnly.MinValue));
        var rows = await db.Appointments.AsNoTracking().Where(x => ids.Contains(x.PersonId) && doctors.Contains(x.DoctorId) && x.StartsAt < end && x.EndsAt > start).OrderBy(x => x.StartsAt).Take(1000).ToListAsync(ct);
        return rows.Select(x => (object)new
        {
            id = x.Id.ToString(CultureInfo.InvariantCulture),
            title = people[x.PersonId] + " · " + (x.Status switch { AppointmentStatus.Attended => "Atendida", AppointmentStatus.NoShow => "No asistió", AppointmentStatus.Cancelled => "Cancelada", _ => "Programada" }),
            start = clock.Local(x.StartsAt).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            end = clock.Local(x.EndsAt).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            color = x.Status switch { AppointmentStatus.Attended => "#237b54", AppointmentStatus.Cancelled => "#74818a", AppointmentStatus.NoShow => "#a44f36", _ => "#147d91" }
        }).ToList();
    }
}
