using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Domain.ValueObjects;

/// <summary>A recurring weekly working window, for example Mondays from 09:00 to 12:00.</summary>
public sealed record WeeklySlot
{
    private WeeklySlot(DayOfWeek day, TimeOnly from, TimeOnly to)
    {
        Day = day;
        From = from;
        To = to;
    }

    public DayOfWeek Day { get; }

    public TimeOnly From { get; }

    public TimeOnly To { get; }

    public static Result<WeeklySlot> Create(DayOfWeek day, TimeOnly from, TimeOnly to)
    {
        if (from >= to)
        {
            return ProfessionalErrors.InvalidSlot;
        }

        return new WeeklySlot(day, from, to);
    }

    /// <summary>True when the whole range falls inside this slot on the matching day of the week.</summary>
    public bool Contains(TimeRange range) =>
        range.Start.DayOfWeek == Day
        && TimeOnly.FromDateTime(range.Start) >= From
        && TimeOnly.FromDateTime(range.End) <= To;

    /// <summary>Slots overlap when they are on the same day and cross in time. Back to back is not an overlap.</summary>
    public bool Overlaps(WeeklySlot other) => Day == other.Day && From < other.To && other.From < To;
}
