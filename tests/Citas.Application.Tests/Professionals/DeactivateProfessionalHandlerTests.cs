using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Professionals;

public class DeactivateProfessionalHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private DeactivateProfessionalHandler CreateHandler() =>
        new(new DeactivateProfessionalValidator(), _professionals, _unitOfWork);

    [Fact]
    public async Task HandleAsync_ActiveProfessional_DeactivatesAndSaves()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);

        var result = await CreateHandler().HandleAsync(
            new DeactivateProfessionalCommand(professional.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(professional.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_AlreadyInactive_StillSucceeds()
    {
        var professional = TestData.NewProfessional();
        professional.Deactivate();
        _professionals.Items.Add(professional);

        var result = await CreateHandler().HandleAsync(
            new DeactivateProfessionalCommand(professional.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(professional.IsActive);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFoundWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(
            new DeactivateProfessionalCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new DeactivateProfessionalCommand(Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_PropagatesTheStoreError()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);
        _unitOfWork.FailWith = ProfessionalErrors.NotFound;

        var result = await CreateHandler().HandleAsync(
            new DeactivateProfessionalCommand(professional.Id), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
    }
}
