using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Domain.ValueObjects;

/// <summary>
/// A span of local clinic time (DateTime with Kind = Unspecified) on a single day.
/// Starts on a quarter hour and lasts 15 to 120 minutes, in steps of 15.
/// </summary>
public sealed record TimeRange
{
    private const int MinMinutes = 15;
    private const int MaxMinutes = 120;
    private const int StepMinutes = 15;

    private TimeRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Duration => End - Start;

    public static Result<TimeRange> Create(DateTime start, TimeSpan duration)
    {
        bool wholeMinutes = duration.Ticks % TimeSpan.TicksPerMinute == 0;
        double minutes = duration.TotalMinutes;
        if (!wholeMinutes || minutes < MinMinutes || minutes > MaxMinutes || minutes % StepMinutes != 0)
        {
            return TimeRangeErrors.InvalidDuration;
        }

        bool onQuarterHour = start.Ticks % TimeSpan.TicksPerMinute == 0 && start.Minute % StepMinutes == 0;
        if (!onQuarterHour)
        {
            return TimeRangeErrors.InvalidStart;
        }

        DateTime end = start + duration;
        if (end.Date != start.Date)
        {
            return TimeRangeErrors.CrossesMidnight;
        }

        return new TimeRange(start, end);
    }

    /// <summary>Two ranges overlap when each starts before the other ends. Touching edges do not overlap.</summary>
    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;
}
