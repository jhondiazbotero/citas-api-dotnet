namespace Citas.Application.Appointments;

/// <summary>A free start time for the requested duration, in local clinic time.</summary>
public sealed record SlotResponse(DateTime Start, DateTime End);
