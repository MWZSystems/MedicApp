using MedicApp.Data;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

[Authorize(Policy = "Integration")]
[ApiController]
[Route("Labor/[action]")]
public class LaborController(MedicDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Events(int after = 0, int limit = 100, CancellationToken ct = default)
    {
        if (after < 0 || limit < 1 || limit > 500) { return BadRequest(); }
        // Consumers persist a per-leave revision; identity gaps are possible after rollback.
        var events = await db.LaborEvents.AsNoTracking().Where(x => x.Id > after).OrderBy(x => x.Id).Take(limit).Select(x => new { eventId = x.Id, leaveId = x.MedicalLeaveId, personId = x.PersonId, revision = x.Revision, startsAtUtc = DateTime.SpecifyKind(x.StartsAt, DateTimeKind.Utc), endsAtUtc = DateTime.SpecifyKind(x.EndsAt, DateTimeKind.Utc), status = x.Status.ToString() }).ToListAsync(ct);
        return Json(new { events, next = events.Count == 0 ? after : events[^1].eventId });
    }
    [HttpGet]
    public async Task<IActionResult> Restrictions(int personId, DateTime from, DateTime to, CancellationToken ct)
    {
        if (personId <= 0 || from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || to <= from || (to - from).TotalDays > 93) { return BadRequest(); }
        return Json(await db.Leaves.AsNoTracking().Where(x => x.PersonId == personId && x.Available && x.Status == Models.Entities.LeaveStatus.Approved && x.StartsAt < to && x.EndsAt > from).OrderBy(x => x.StartsAt).Select(x => new { leaveId = x.Id, personId = x.PersonId, revision = x.Revision, startsAtUtc = DateTime.SpecifyKind(x.StartsAt, DateTimeKind.Utc), endsAtUtc = DateTime.SpecifyKind(x.EndsAt, DateTimeKind.Utc) }).ToListAsync(ct));
    }
}
