using Citas.Application.Abstractions;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeClinicClock(DateTime now) : IClinicClock
{
    public DateTime Now { get; set; } = now;
}
