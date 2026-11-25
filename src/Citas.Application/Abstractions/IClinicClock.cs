namespace Citas.Application.Abstractions;

/// <summary>The current moment in the clinic's local time (DateTime with Kind = Unspecified).</summary>
public interface IClinicClock
{
    DateTime Now { get; }
}
