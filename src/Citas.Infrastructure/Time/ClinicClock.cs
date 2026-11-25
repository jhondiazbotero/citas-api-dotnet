using Citas.Application.Abstractions;

namespace Citas.Infrastructure.Time;

/// <summary>The clinic's wall clock: UTC from the <see cref="TimeProvider"/> converted to the clinic time zone, Kind = Unspecified.</summary>
public sealed class ClinicClock(TimeProvider timeProvider, TimeZoneInfo timeZone) : IClinicClock
{
    public DateTime Now
    {
        get
        {
            DateTime local = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime;
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }
}
