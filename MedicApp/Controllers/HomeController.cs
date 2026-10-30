using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

public class HomeController(MedicDbContext db, MedicalAccess access, MedicalClock clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var person = access.PersonId;
        var doctor = await access.DoctorIdAsync(ct);
        ViewBag.Appointments = await db.Appointments.AsNoTracking().CountAsync(x => x.Available && (x.PersonId == person || x.DoctorId == doctor) && x.Status == AppointmentStatus.Scheduled && x.EndsAt > clock.UtcNow, ct);
        ViewBag.Leaves = await db.Leaves.AsNoTracking().CountAsync(x => x.Available && (x.PersonId == person || x.DoctorId == doctor) && x.Status == LeaveStatus.Approved && x.EndsAt > clock.UtcNow, ct);
        ViewBag.IsDoctor = doctor != null;
        return View();
    }
    [AllowAnonymous] public IActionResult Error() { Response.StatusCode = 500; return View(); }
}
