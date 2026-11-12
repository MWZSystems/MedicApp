using System.Globalization;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using MWZSystem.Ui;
namespace MedicApp.Services;

/// <summary>Tablero de Inicio. Cada parte queda vacía o en null cuando la persona no puede consultarla.</summary>
public sealed record MedicalDashboard(IReadOnlyList<MwzStat> Mine, IReadOnlyList<MwzStat> Doctor, MwzBars? DoctorWeek,
    IReadOnlyList<MwzStat> Clinic, MwzBars? ClinicMonth, MwzSeries? ClinicWeeks, MwzStat? LowStock);

/// <summary>
/// Solo conteos por estado y fecha calculados en SQL. No consulta expedientes, visitas clínicas ni documentos:
/// el tablero no puede mostrar diagnósticos ni motivos de consulta.
/// </summary>
public sealed class MedicalDashboardService(MedicDbContext db, MedicalAccess access, MedicalClock clock)
{
    private const int Weeks = 6;
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    /// <summary>Semana (lunes a domingo) y mes actuales de la clínica, como instantes UTC [inicio, fin).</summary>
    public static (DateTime WeekStart, DateTime WeekEnd, DateTime MonthStart, DateTime MonthEnd) Ranges(MedicalClock clock)
    {
        var today = clock.Local(clock.UtcNow).Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var month = new DateTime(today.Year, today.Month, 1);
        return (Utc(clock, monday), Utc(clock, monday.AddDays(7)), Utc(clock, month), Utc(clock, month.AddMonths(1)));
    }

    // Medianoche local a UTC; si el cambio de horario hace inválida esa hora, se toma la siguiente hora válida.
    private static DateTime Utc(MedicalClock clock, DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (clock.Zone.IsInvalidTime(local)) { local = local.AddHours(1); }
        return TimeZoneInfo.ConvertTimeToUtc(local, clock.Zone);
    }

    public async Task<MedicalDashboard> BuildAsync(Func<string, string> text, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var person = access.PersonId;
        var doctor = await access.DoctorIdAsync(ct);
        var (weekStart, weekEnd, monthStart, monthEnd) = Ranges(clock);
        var today = clock.Local(now).Date;
        DateTime dayStart = Utc(clock, today), dayEnd = Utc(clock, today.AddDays(1));
        var appointments = db.Appointments.AsNoTracking().Where(x => x.Available);
        string Number(int value) => value.ToString("N0", Culture);
        MwzBars ByStatus(string title, Dictionary<AppointmentStatus, int> rows) => new(text(title),
        [
            new(text("Scheduled"), rows.GetValueOrDefault(AppointmentStatus.Scheduled)), new(text("Attended"), rows.GetValueOrDefault(AppointmentStatus.Attended), MwzTone.Ok),
            new(text("Cancelled"), rows.GetValueOrDefault(AppointmentStatus.Cancelled), MwzTone.Aviso), new(text("NoShow"), rows.GetValueOrDefault(AppointmentStatus.NoShow), MwzTone.Peligro)
        ], Stacked: true, EmptyText: text("DashNoAppointments"));

        // Lo propio: misma definición que mostraba Inicio (como paciente o como médico de la cita).
        List<MwzStat> mine =
        [
            new(text("PendingAppointments"), Number(await appointments.CountAsync(x => (x.PersonId == person || x.DoctorId == doctor) && x.Status == AppointmentStatus.Scheduled && x.EndsAt > now, ct)), "calendar-event", "/Appointments"),
            new(text("ActiveLeaves"), Number(await db.Leaves.AsNoTracking().CountAsync(x => x.Available && (x.PersonId == person || x.DoctorId == doctor) && x.Status == LeaveStatus.Approved && x.EndsAt > now, ct)), "file-earmark-check", "/Leaves")
        ];

        List<MwzStat> doctorStats = [];
        MwzBars? doctorWeek = null;
        if (doctor != null)
        {
            var own = appointments.Where(x => x.DoctorId == doctor);
            var week = await own.Where(x => x.StartsAt >= weekStart && x.StartsAt < weekEnd).GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            doctorStats =
            [
                new(text("DashToday"), Number(await own.CountAsync(x => x.StartsAt >= dayStart && x.StartsAt < dayEnd && x.Status != AppointmentStatus.Cancelled, ct)), "calendar-day", "/Agenda"),
                new(text("DashWeek"), Number(week.Where(x => x.Key != AppointmentStatus.Cancelled).Sum(x => x.Value)), "calendar-week", "/Agenda")
            ];
            doctorWeek = ByStatus("DashMyWeek", week);
        }

        List<MwzStat> clinic = [];
        MwzBars? clinicMonth = null;
        MwzSeries? clinicWeeks = null;
        var manageDoctors = await access.CanManageDoctorsAsync(ct);
        if (manageDoctors || await access.CanManageAccessAsync(ct))
        {
            var month = await appointments.Where(x => x.StartsAt >= monthStart && x.StartsAt < monthEnd).GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            clinic =
            [
                new(text("DashMonth"), Number(month.Sum(x => x.Value)), "calendar3"),
                new(text("ActiveLeaves"), Number(await db.Leaves.AsNoTracking().CountAsync(x => x.Available && x.Status == LeaveStatus.Approved && x.EndsAt > now, ct)), "file-earmark-check"),
                new(text("DashDoctors"), Number(await db.Doctors.AsNoTracking().CountAsync(x => x.Available, ct)), "person-badge", manageDoctors ? "/Admin/Doctors" : null)
            ];
            clinicMonth = ByStatus("DashClinicMonth", month);
            // Seis conteos, uno por semana, con los límites de la semana local de la clínica.
            var points = new List<MwzPoint>();
            for (var i = Weeks - 1; i >= 0; i--)
            {
                DateTime start = weekStart.AddDays(-7 * i), end = start.AddDays(7);
                points.Add(new(string.Format(Culture, text("DashWeekOf"), clock.Local(start)), await appointments.CountAsync(x => x.StartsAt >= start && x.StartsAt < end, ct)));
            }
            clinicWeeks = new(text("DashClinicWeeks"), points, text("DashNoAppointments"));
        }

        MwzStat? lowStock = null;
        if (await access.CanUseInventoryAsync(ct))
        {
            var low = await db.StockItems.AsNoTracking().CountAsync(x => x.Available && x.CurrentStock < x.MinimumStock, ct);
            lowStock = new(text("DashLowStock"), Number(low), "box-seam", "/Inventory", low > 0 ? MwzTone.Aviso : MwzTone.Ok);
        }

        return new(mine, doctorStats, doctorWeek, clinic, clinicMonth, clinicWeeks, lowStock);
    }
}
