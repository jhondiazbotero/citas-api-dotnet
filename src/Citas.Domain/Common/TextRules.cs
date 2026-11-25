namespace Citas.Domain.Common;

/// <summary>Small text checks shared by the entities.</summary>
internal static class TextRules
{
    /// <summary>Length within bounds, not blank, and no space at either end.</summary>
    public static bool IsTrimmedWithin(string? value, int min, int max) =>
        value is not null
        && value.Length >= min
        && value.Length <= max
        && value == value.Trim();

    /// <summary>Only ASCII digits (0-9), with a length within bounds.</summary>
    public static bool IsDigitsWithin(string? value, int min, int max) =>
        value is not null
        && value.Length >= min
        && value.Length <= max
        && value.All(c => c is >= '0' and <= '9');
}
