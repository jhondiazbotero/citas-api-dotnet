using Citas.Domain.Errors;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Tests.ValueObjects;

public class WeeklySlotTests
{
    // 2026-12-02 is a Wednesday.
    private static readonly DateTime Wednesday = new(2026, 12, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private static TimeRange Range(DateTime day, int hour, int minute, int durationMinutes) =>
        TimeRange.Create(day.AddHours(hour).AddMinutes(minute), TimeSpan.FromMinutes(durationMinutes)).Value;

    private static WeeklySlot Slot(DayOfWeek day, TimeOnly from, TimeOnly to) => WeeklySlot.Create(day, from, to).Value;

    [Fact]
    public void Create_FromBeforeTo_Succeeds()
    {
        var result = WeeklySlot.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(DayOfWeek.Monday, result.Value.Day);
        Assert.Equal(new TimeOnly(9, 0), result.Value.From);
        Assert.Equal(new TimeOnly(12, 0), result.Value.To);
    }

    [Fact]
    public void Create_FromEqualToTo_FailsWithInvalidSlot()
    {
        var result = WeeklySlot.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 0));

        Assert.Equal(ProfessionalErrors.InvalidSlot, result.Error);
    }

    [Fact]
    public void Create_FromAfterTo_FailsWithInvalidSlot()
    {
        var result = WeeklySlot.Create(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(9, 0));

        Assert.Equal(ProfessionalErrors.InvalidSlot, result.Error);
    }

    [Fact]
    public void Contains_RangeExactlyTheSlot_IsTrue()
    {
        var slot = Slot(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 0));

        Assert.True(slot.Contains(Range(Wednesday, 9, 0, 60)));
    }

    [Fact]
    public void Contains_RangeInsideTheSlot_IsTrue()
    {
        var slot = Slot(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.True(slot.Contains(Range(Wednesday, 10, 15, 30)));
    }

    [Fact]
    public void Contains_RangeEndsOneMinutePastTheSlot_IsFalse()
    {
        var slot = Slot(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(11, 59));

        Assert.False(slot.Contains(Range(Wednesday, 11, 0, 60)));
    }

    [Fact]
    public void Contains_RangeStartsOneMinuteBeforeTheSlot_IsFalse()
    {
        var slot = Slot(DayOfWeek.Wednesday, new TimeOnly(9, 1), new TimeOnly(12, 0));

        Assert.False(slot.Contains(Range(Wednesday, 9, 0, 60)));
    }

    [Fact]
    public void Contains_SameHoursOnAnotherDayOfTheWeek_IsFalse()
    {
        var slot = Slot(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.False(slot.Contains(Range(Wednesday, 9, 0, 60)));
    }

    [Fact]
    public void Contains_SameDayOfTheWeekOnAnotherDate_IsTrue()
    {
        var slot = Slot(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.True(slot.Contains(Range(Wednesday.AddDays(7), 9, 0, 60)));
    }

    [Fact]
    public void Overlaps_SameDayCrossingHours_IsTrue()
    {
        var a = Slot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var b = Slot(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(14, 0));

        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Overlaps_SameDayBackToBack_IsFalse()
    {
        var a = Slot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var b = Slot(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(14, 0));

        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_DifferentDaysSameHours_IsFalse()
    {
        var a = Slot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var b = Slot(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.False(a.Overlaps(b));
    }
}
