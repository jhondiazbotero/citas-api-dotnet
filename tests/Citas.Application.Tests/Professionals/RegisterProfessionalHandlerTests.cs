using Citas.Application.Common;
using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Professionals;

public class RegisterProfessionalHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private RegisterProfessionalHandler CreateHandler() =>
        new(new RegisterProfessionalValidator(), _professionals, _unitOfWork);

    private static RegisterProfessionalCommand Command(params WorkingSlotDto[] slots) =>
        new("Dr. Luis Mora", "Cardiology", slots);

    [Fact]
    public async Task HandleAsync_ValidData_AddsProfessionalWithItsScheduleAndSaves()
    {
        var result = await CreateHandler().HandleAsync(Command(TestData.WednesdayMorning), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dr. Luis Mora", result.Value.FullName);
        Assert.True(result.Value.IsActive);
        Assert.Equal([TestData.WednesdayMorning], result.Value.WorkingHours);
        Assert.Single(_professionals.Items);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_SlotEndsBeforeItStarts_PropagatesInvalidSlotWithoutSaving()
    {
        var backwards = new WorkingSlotDto(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(9, 0));

        var result = await CreateHandler().HandleAsync(Command(backwards), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.InvalidSlot, result.Error);
        Assert.Empty(_professionals.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_OverlappingSlots_PropagatesOverlappingWorkingHoursWithoutSaving()
    {
        var other = new WorkingSlotDto(DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(13, 0));

        var result = await CreateHandler().HandleAsync(Command(TestData.WednesdayMorning, other), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.OverlappingWorkingHours, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NoSlots_PropagatesNoWorkingHoursWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NoWorkingHours, result.Error);
        Assert.Empty(_professionals.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_DomainRejectsName_PropagatesErrorWithoutSaving()
    {
        var command = new RegisterProfessionalCommand(" ", "Cardiology", [TestData.WednesdayMorning]);

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.Equal(ProfessionalErrors.InvalidName, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NullSpecialty_FailsWithValidationError()
    {
        var command = new RegisterProfessionalCommand("Dr. Luis Mora", null!, [TestData.WednesdayMorning]);

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NullWorkingHours_FailsWithValidationError()
    {
        var command = new RegisterProfessionalCommand("Dr. Luis Mora", "Cardiology", null!);

        var result = await CreateHandler().HandleAsync(command, CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_DayOfWeekOutOfRange_FailsWithValidationError()
    {
        var weird = new WorkingSlotDto((DayOfWeek)99, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var result = await CreateHandler().HandleAsync(Command(weird), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }
}
