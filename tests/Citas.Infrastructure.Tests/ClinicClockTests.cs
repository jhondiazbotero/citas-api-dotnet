using Citas.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace Citas.Infrastructure.Tests;

public class ClinicClockTests
{
    private static ClinicClock ClockAt(DateTimeOffset utc, string zoneId = "America/Bogota")
    {
        var provider = new FakeTimeProvider(utc);
        return new ClinicClock(provider, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
    }

    [Fact]
    public void Now_IsBogotaTime()
    {
        // Bogota is UTC-5 all year.
        var clock = ClockAt(new DateTimeOffset(2026, 12, 1, 13, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 12, 1, 8, 30, 0), clock.Now);
    }

    [Fact]
    public void Now_HasKindUnspecified()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 12, 1, 13, 30, 0, TimeSpan.Zero));

        Assert.Equal(DateTimeKind.Unspecified, clock.Now.Kind);
    }

    [Fact]
    public void Now_JustAfterMidnightUtc_IsStillThePreviousDayInBogota()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 12, 2, 1, 15, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 12, 1, 20, 15, 0), clock.Now);
    }

    [Fact]
    public void Now_FollowsTheProvider_WhenTimePasses()
    {
        var provider = new FakeTimeProvider(new DateTimeOffset(2026, 12, 1, 13, 0, 0, TimeSpan.Zero));
        var clock = new ClinicClock(provider, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));

        provider.Advance(TimeSpan.FromMinutes(45));

        Assert.Equal(new DateTime(2026, 12, 1, 8, 45, 0), clock.Now);
    }

    [Fact]
    public void Now_UsesTheConfiguredZone()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 12, 1, 13, 30, 0, TimeSpan.Zero), "Asia/Tokyo");

        Assert.Equal(new DateTime(2026, 12, 1, 22, 30, 0), clock.Now);
    }
}
