using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Professionals;

namespace Citas.Application.Tests.Professionals;

public class ListActiveProfessionalsHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();

    private ListActiveProfessionalsHandler CreateHandler() => new(_professionals);

    private Professional Add(string name, string specialty = "Cardiology", bool active = true)
    {
        var professional = Professional.Create(name, specialty).Value;
        if (!active)
        {
            professional.Deactivate();
        }

        _professionals.Items.Add(professional);
        return professional;
    }

    [Fact]
    public async Task HandleAsync_NoProfessionals_ReturnsEmptyList()
    {
        var result = await CreateHandler().HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task HandleAsync_ReturnsIdNameAndSpecialty()
    {
        var professional = Add("Dr. Luis Mora", "Dermatology");

        var result = await CreateHandler().HandleAsync(CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal(new ProfessionalSummaryResponse(professional.Id, "Dr. Luis Mora", "Dermatology"), item);
    }

    [Fact]
    public async Task HandleAsync_LeavesOutInactiveProfessionals()
    {
        var active = Add("Dr. Luis Mora");
        Add("Dra. Ines Rua", active: false);

        var result = await CreateHandler().HandleAsync(CancellationToken.None);

        Assert.Equal([active.Id], result.Value.Select(p => p.Id));
    }

    [Fact]
    public async Task HandleAsync_OrdersByNameWithoutCaringAboutCase()
    {
        Add("Zeta Perez");
        Add("alfa Ruiz");
        Add("Beta Gomez");

        var result = await CreateHandler().HandleAsync(CancellationToken.None);

        Assert.Equal(["alfa Ruiz", "Beta Gomez", "Zeta Perez"], result.Value.Select(p => p.FullName));
    }

    [Fact]
    public async Task HandleAsync_ResponseCarriesNoScheduleOrStatus()
    {
        string[] properties = [.. typeof(ProfessionalSummaryResponse).GetProperties().Select(p => p.Name).Order()];

        Assert.Equal(["FullName", "Id", "Specialty"], properties);
        await Task.CompletedTask;
    }
}
