using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Professionals;

public class GetProfessionalHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();

    private GetProfessionalHandler CreateHandler() => new(new GetProfessionalValidator(), _professionals);

    [Fact]
    public async Task HandleAsync_ExistingProfessional_ReturnsResponseWithSchedule()
    {
        var professional = TestData.NewProfessional();
        _professionals.Items.Add(professional);

        var result = await CreateHandler().HandleAsync(
            new GetProfessionalQuery(professional.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(professional.Id, result.Value.Id);
        Assert.Equal("Cardiology", result.Value.Specialty);
        Assert.Equal(2, result.Value.WorkingHours.Count);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProfessionalQuery(Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }
}
