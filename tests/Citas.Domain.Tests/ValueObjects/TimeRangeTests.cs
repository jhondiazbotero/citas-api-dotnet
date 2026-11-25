using Citas.Domain.Errors;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Tests.ValueObjects;

public class TimeRangeTests
{
    // Wednesday, local clinic time (Kind = Unspecified).
    private static readonly DateTime Day = new(2026, 12, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private static DateTime At(int hour, int minute, int second = 0) => Day.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    private static TimeRange Range(int hour, int minute, int durationMinutes) =>
        TimeRange.Create(At(hour, minute), TimeSpan.FromMinutes(durationMinutes)).Value;

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(120)]
    public void Create_ValidDuration_Succeeds(int minutes)
    {
        var result = TimeRange.Create(At(9, 0), TimeSpan.FromMinutes(minutes));

        Assert.True(result.IsSuccess);
        Assert.Equal(At(9, 0), result.Value.Start);
        Assert.Equal(At(9, 0).AddMinutes(minutes), result.Value.End);
        Assert.Equal(TimeSpan.FromMinutes(minutes), result.Value.Duration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(14)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(121)]
    [InlineData(135)]
    public void Create_InvalidDuration_FailsWithInvalidDuration(int minutes)
    {
        var result = TimeRange.Create(At(9, 0), TimeSpan.FromMinutes(minutes));

        Assert.True(result.IsFailure);
        Assert.Equal(TimeRangeErrors.InvalidDuration, result.Error);
    }

    [Fact]
    public void Create_DurationWithSeconds_FailsWithInvalidDuration()
    {
        var result = TimeRange.Create(At(9, 0), TimeSpan.FromSeconds(15 * 60 + 1));

        Assert.Equal(TimeRangeErrors.InvalidDuration, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void Create_StartOnQuarterHour_Succeeds(int minute)
    {
        Assert.True(TimeRange.Create(At(9, minute), TimeSpan.FromMinutes(30)).IsSuccess);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(59)]
    public void Create_StartNotOnQuarterHour_FailsWithInvalidStart(int minute)
    {
        var result = TimeRange.Create(At(9, minute), TimeSpan.FromMinutes(30));

        Assert.Equal(TimeRangeErrors.InvalidStart, result.Error);
    }

    [Fact]
    public void Create_StartWithSeconds_FailsWithInvalidStart()
    {
        var result = TimeRange.Create(At(9, 0, 1), TimeSpan.FromMinutes(30));

        Assert.Equal(TimeRangeErrors.InvalidStart, result.Error);
    }

    [Fact]
    public void Create_StartWithMilliseconds_FailsWithInvalidStart()
    {
        var result = TimeRange.Create(At(9, 0).AddMilliseconds(1), TimeSpan.FromMinutes(30));

        Assert.Equal(TimeRangeErrors.InvalidStart, result.Error);
    }

    [Fact]
    public void Create_CrossingMidnight_FailsWithCrossesMidnight()
    {
        var result = TimeRange.Create(At(23, 30), TimeSpan.FromMinutes(60));

        Assert.Equal(TimeRangeErrors.CrossesMidnight, result.Error);
    }

    [Fact]
    public void Create_EndingExactlyAtMidnight_FailsWithCrossesMidnight()
    {
        // End lands on 00:00 of the next day, so start and end are not on the same day.
        var result = TimeRange.Create(At(23, 45), TimeSpan.FromMinutes(15));

        Assert.Equal(TimeRangeErrors.CrossesMidnight, result.Error);
    }

    [Fact]
    public void Create_EndingAtLastQuarterOfTheDay_Succeeds()
    {
        Assert.True(TimeRange.Create(At(23, 30), TimeSpan.FromMinutes(15)).IsSuccess);
    }

    [Theory]
    [InlineData(23, 45, 15)]
    [InlineData(23, 30, 120)]
    [InlineData(23, 0, 60)]
    public void Create_StartNearDateTimeMaxValue_FailsWithOutOfRangeInsteadOfThrowing(int hour, int minute, int durationMinutes)
    {
        var start = new DateTime(9999, 12, 31, hour, minute, 0, DateTimeKind.Unspecified);

        var result = TimeRange.Create(start, TimeSpan.FromMinutes(durationMinutes));

        Assert.True(result.IsFailure);
        Assert.Equal(TimeRangeErrors.OutOfRange, result.Error);
    }

    [Fact]
    public void Create_LastQuarterHourThatStillFitsBeforeMaxValue_Succeeds()
    {
        var start = new DateTime(9999, 12, 31, 23, 30, 0, DateTimeKind.Unspecified);

        Assert.True(TimeRange.Create(start, TimeSpan.FromMinutes(15)).IsSuccess);
    }

    [Fact]
    public void Overlaps_EndsExactlyWhenOtherStarts_IsFalse()
    {
        var first = Range(9, 0, 30);
        var second = Range(9, 30, 30);

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    [Fact]
    public void Overlaps_PartialIntersection_IsTrue()
    {
        var first = Range(9, 0, 30);
        var second = Range(9, 15, 30);

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }

    [Fact]
    public void Overlaps_OneContainsTheOther_IsTrue()
    {
        var outer = Range(9, 0, 120);
        var inner = Range(9, 30, 15);

        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    [Fact]
    public void Overlaps_SameRange_IsTrue()
    {
        Assert.True(Range(9, 0, 30).Overlaps(Range(9, 0, 30)));
    }

    [Fact]
    public void Overlaps_Disjoint_IsFalse()
    {
        Assert.False(Range(9, 0, 30).Overlaps(Range(10, 0, 30)));
    }

    [Fact]
    public void Overlaps_SameTimeOnDifferentDays_IsFalse()
    {
        var today = Range(9, 0, 30);
        var tomorrow = TimeRange.Create(At(9, 0).AddDays(1), TimeSpan.FromMinutes(30)).Value;

        Assert.False(today.Overlaps(tomorrow));
    }

    [Fact]
    public void Equality_SameStartAndEnd_AreEqual()
    {
        Assert.Equal(Range(9, 0, 30), Range(9, 0, 30));
        Assert.NotEqual(Range(9, 0, 30), Range(9, 0, 45));
    }
}
