namespace Citas.Application.Common;

/// <summary>A weekly working window; used both to receive and to return a professional's schedule.</summary>
public sealed record WorkingSlotDto(DayOfWeek Day, TimeOnly From, TimeOnly To);
