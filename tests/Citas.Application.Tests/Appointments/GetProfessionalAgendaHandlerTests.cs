using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class GetProfessionalAgendaHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly Domain.Professionals.Professional _professional = TestData.NewProfessional();

    public GetProfessionalAgendaHandlerTests() => _professionals.Items.Add(_professional);

    private GetProfessionalAgendaHandler CreateHandler() =>
        new(new GetProfessionalAgendaValidator(), _professionals, _appointments);

    private static DateOnly Day(int dayOfDecember) => new(2026, 12, dayOfDecember);

    [Fact]
    public async Task HandleAsync_AppointmentsInsideTheRange_ReturnsThemSortedByStart()
    {
        var late = TestData.Book(_professional, TestData.NewPatient("1111111"), TestData.At(2, 15, 0));
        var early = TestData.Book(_professional, TestData.NewPatient("2222222"), TestData.At(2, 9, 0));
        _appointments.Items.AddRange([late, early]);

        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(2), Day(2)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([early.Id, late.Id], result.Value.Select(a => a.Id));
    }

    [Fact]
    public async Task HandleAsync_AppointmentsOutsideTheRangeOrOfOthers_AreLeftOut()
    {
        var other = TestData.NewProfessional();
        _appointments.Items.Add(TestData.Book(_professional, TestData.NewPatient("1111111"), TestData.At(9, 10, 0)));
        _appointments.Items.Add(TestData.Book(other, TestData.NewPatient("2222222"), TestData.At(2, 10, 0)));

        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(2), Day(3)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_CancelledAppointment_IsListedWithItsStatus()
    {
        var appointment = TestData.Book(_professional, TestData.NewPatient(), TestData.At(2, 10, 0));
        appointment.Cancel("Changed plans", TestData.At(1, 8, 0), byClinic: true);
        _appointments.Items.Add(appointment);

        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(2), Day(2)), CancellationToken.None);

        Assert.Equal("Cancelled", Assert.Single(result.Value).Status);
    }

    [Fact]
    public async Task HandleAsync_ExactlyThirtyOneDays_Succeeds()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(1), Day(31)), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_ThirtyTwoDays_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(1), new DateOnly(2027, 1, 1)), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_EndBeforeStart_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(_professional.Id, Day(5), Day(4)), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_EmptyProfessionalId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(Guid.Empty, Day(1), Day(2)), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalAgendaQuery(Guid.NewGuid(), Day(1), Day(2)), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
    }
}
