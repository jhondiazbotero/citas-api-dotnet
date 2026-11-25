using Citas.Application.Common;
using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Professionals;

public class SetWorkingHoursHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SetWorkingHoursHandler CreateHandler() =>
        new(new SetWorkingHoursValidator(), _professionals, _unitOfWork);

    [Fact]
    public async Task HandleAsync_ValidSlots_ReplacesScheduleAndSaves()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);
        var friday = new WorkingSlotDto(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(10, 0));

        var result = await CreateHandler().HandleAsync(
            new SetWorkingHoursCommand(professional.Id, [friday]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([friday], result.Value.WorkingHours);
        Assert.Single(professional.WorkingHours);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new SetWorkingHoursCommand(Guid.NewGuid(), [TestData.WednesdayMorning]), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_OverlappingSlots_PropagatesErrorKeepsOldScheduleAndDoesNotSave()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);
        var overlapping = new WorkingSlotDto(DayOfWeek.Wednesday, new TimeOnly(11, 0), new TimeOnly(13, 0));

        var result = await CreateHandler().HandleAsync(
            new SetWorkingHoursCommand(professional.Id, [TestData.WednesdayMorning, overlapping]),
            CancellationToken.None);

        Assert.Equal(ProfessionalErrors.OverlappingWorkingHours, result.Error);
        Assert.Equal(2, professional.WorkingHours.Count);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyList_PropagatesNoWorkingHoursWithoutSaving()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);

        var result = await CreateHandler().HandleAsync(
            new SetWorkingHoursCommand(professional.Id, []), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NoWorkingHours, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new SetWorkingHoursCommand(Guid.Empty, [TestData.WednesdayMorning]), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }
}
