using Microsoft.Extensions.Options;
namespace MedicApp.Services;

public class MedicalClock(IOptions<MedicalOptions> options, TimeProvider time)
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public DateTime ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
        {
            throw new MedicalRuleException("InvalidTime");
        }
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }
}
