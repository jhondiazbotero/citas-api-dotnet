using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class GetAvailableSlotsHandlerTests
{
    private static readonly DateOnly Wednesday = new(2026, 12, 2);

    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeClinicClock _clock = new(TestData.Now);
    private readonly Domain.Professionals.Professional _professional = TestData.NewProfessional();

    public GetAvailableSlotsHandlerTests() => _professionals.Items.Add(_professional);

    private GetAvailableSlotsHandler CreateHandler() =>
        new(new GetAvailableSlotsValidator(), _professionals, _appointments, _clock);

    private async Task<List<TimeOnly>> StartsAsync(DateOnly date, int minutes, Guid? professionalId = null)
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(professionalId ?? _professional.Id, date, minutes), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return [.. result.Value.Select(s => TimeOnly.FromDateTime(s.Start))];
    }

    private static List<TimeOnly> Range(TimeOnly first, TimeOnly last)
    {
        List<TimeOnly> times = [];
        for (var t = first; t <= last; t = t.AddMinutes(15))
        {
            times.Add(t);
        }

        return times;
    }

    [Fact]
    public async Task HandleAsync_DayWithoutWorkingHours_ReturnsEmptyList()
    {
        var thursday = new DateOnly(2026, 12, 3);

        Assert.Empty(await StartsAsync(thursday, 30));
    }

    [Fact]
    public async Task HandleAsync_EmptyDay_ListsEveryQuarterHourThatFitsInEachWorkingSlot()
    {
        // Working 09:00-12:00 and 14:00-17:00, 30-minute visits: last starts are 11:30 and 16:30.
        var expected = Range(new TimeOnly(9, 0), new TimeOnly(11, 30))
            .Concat(Range(new TimeOnly(14, 0), new TimeOnly(16, 30)));

        Assert.Equal(expected, await StartsAsync(Wednesday, 30));
    }

    [Fact]
    public async Task HandleAsync_Response_CarriesTheEndOfEachSlot()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(_professional.Id, Wednesday, 45), CancellationToken.None);

        var first = result.Value[0];
        Assert.Equal(TestData.At(2, 9, 0), first.Start);
        Assert.Equal(TestData.At(2, 9, 45), first.End);
    }

    [Fact]
    public async Task HandleAsync_AppointmentInTheMiddle_SplitsTheWorkingSlot()
    {
        // Booked 10:00-10:30: 09:45 would run into it, 10:30 is free again.
        _appointments.Items.Add(TestData.Book(_professional, TestData.NewPatient(), TestData.At(2, 10, 0)));

        var starts = await StartsAsync(Wednesday, 30);

        Assert.Contains(new TimeOnly(9, 0), starts);
        Assert.Contains(new TimeOnly(9, 30), starts);
        Assert.DoesNotContain(new TimeOnly(9, 45), starts);
        Assert.DoesNotContain(new TimeOnly(10, 0), starts);
        Assert.DoesNotContain(new TimeOnly(10, 15), starts);
        Assert.Contains(new TimeOnly(10, 30), starts);
    }

    [Fact]
    public async Task HandleAsync_CancelledAppointment_DoesNotBlock()
    {
        var appointment = TestData.Book(_professional, TestData.NewPatient(), TestData.At(2, 10, 0));
        appointment.Cancel("Changed plans", TestData.At(1, 8, 0), byClinic: true);
        _appointments.Items.Add(appointment);

        var starts = await StartsAsync(Wednesday, 30);

        Assert.Contains(new TimeOnly(10, 0), starts);
        Assert.Contains(new TimeOnly(9, 45), starts);
    }

    [Fact]
    public async Task HandleAsync_AnotherProfessionalsAppointment_DoesNotBlock()
    {
        var other = TestData.NewProfessional();
        _appointments.Items.Add(TestData.Book(other, TestData.NewPatient(), TestData.At(2, 10, 0)));

        Assert.Contains(new TimeOnly(10, 0), await StartsAsync(Wednesday, 30));
    }

    [Fact]
    public async Task HandleAsync_SameDayWithOneHourLeadTime_SkipsStartsLessThanAnHourAway()
    {
        _clock.Now = TestData.At(2, 8, 30);

        var starts = await StartsAsync(Wednesday, 30);

        Assert.DoesNotContain(new TimeOnly(9, 15), starts);
        Assert.Equal(new TimeOnly(9, 30), starts[0]);
    }

    [Fact]
    public async Task HandleAsync_DateAlreadyPast_ReturnsEmptyList()
    {
        _clock.Now = TestData.At(3, 8, 0);

        Assert.Empty(await StartsAsync(Wednesday, 30));
    }

    [Fact]
    public async Task HandleAsync_DurationThatDoesNotFitAtTheEndOfTheSlot_StopsEarlier()
    {
        // 120-minute visit in 09:00-12:00: only 09:00, 09:15 ... 10:00 fit; 10:15 would end at 12:15.
        var starts = await StartsAsync(Wednesday, 120);

        Assert.Equal(new TimeOnly(10, 0), starts.Last(t => t < new TimeOnly(12, 0)));
        Assert.DoesNotContain(new TimeOnly(10, 15), starts);
    }

    [Fact]
    public async Task HandleAsync_BackToBackWorkingSlots_OffersStartsThatCrossTheJoin()
    {
        // Working 08:00-12:00 and 12:00-16:00 is one stretch: with 60 minutes, 11:15, 11:30 and 11:45 fit.
        var professional = Domain.Professionals.Professional.Create("Dr. Ines Rua", "Pediatrics").Value;
        professional.SetWorkingHours(
        [
            Domain.ValueObjects.WeeklySlot.Create(DayOfWeek.Wednesday, new TimeOnly(8, 0), new TimeOnly(12, 0)).Value,
            Domain.ValueObjects.WeeklySlot.Create(DayOfWeek.Wednesday, new TimeOnly(12, 0), new TimeOnly(16, 0)).Value,
        ]);
        _professionals.Items.Add(professional);

        var starts = await StartsAsync(Wednesday, 60, professional.Id);

        Assert.Contains(new TimeOnly(11, 15), starts);
        Assert.Contains(new TimeOnly(11, 30), starts);
        Assert.Contains(new TimeOnly(11, 45), starts);
        Assert.Equal(Range(new TimeOnly(8, 0), new TimeOnly(15, 0)), starts);
    }

    [Fact]
    public async Task HandleAsync_InactiveProfessional_ReturnsEmptyList()
    {
        _professional.Deactivate();

        Assert.Empty(await StartsAsync(Wednesday, 30));
    }

    [Fact]
    public async Task HandleAsync_DurationNotInSteps_PropagatesInvalidDuration()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(_professional.Id, Wednesday, 20), CancellationToken.None);

        Assert.Equal(TimeRangeErrors.InvalidDuration, result.Error);
    }

    [Fact]
    public async Task HandleAsync_ZeroDuration_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(_professional.Id, Wednesday, 0), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(Guid.NewGuid(), Wednesday, 30), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyProfessionalId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(Guid.Empty, Wednesday, 30), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_DefaultDate_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAvailableSlotsQuery(_professional.Id, default, 30), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_EveryReturnedSlot_IsAcceptedByTheDomainWhenScheduling()
    {
        // Cross-check against the real booking rules: each offered start can be scheduled for real.
        _appointments.Items.Add(TestData.Book(_professional, TestData.NewPatient(), TestData.At(2, 10, 0)));
        var patient = TestData.NewPatient("9999999");

        var offered = await StartsAsync(Wednesday, 30);
        Assert.NotEmpty(offered);
        foreach (var start in offered)
        {
            var range = TestData.Range(Wednesday.ToDateTime(start), 30);
            var scheduled = Domain.Appointments.AppointmentScheduler.Schedule(
                _professional, patient, range, null, _appointments.Items, TestData.Now);

            Assert.True(scheduled.IsSuccess, $"{start} was offered but the domain rejects it: {scheduled.Error.Code}");
        }
    }
}
