using Citas.Domain.Common;

namespace Citas.Domain.Errors;

public static class TimeRangeErrors
{
    public static readonly Error InvalidDuration =
        new("TimeRange.InvalidDuration", "Duration must be 15 to 120 minutes, in steps of 15.");

    public static readonly Error InvalidStart =
        new("TimeRange.InvalidStart", "Start must fall on minute 0, 15, 30 or 45 with no seconds.");

    public static readonly Error CrossesMidnight =
        new("TimeRange.CrossesMidnight", "Start and end must be on the same day.");

    public static readonly Error OutOfRange =
        new("TimeRange.OutOfRange", "Start and end must be representable dates.");
}
