using Citas.Domain.Professionals;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Repositories;
using Citas.Infrastructure.Tests.Support;
using Citas.Testing;

namespace Citas.Infrastructure.Tests;

public class ProfessionalRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task ListActive_ReturnsOnlyActiveProfessionals_WithTheirSchedule()
    {
        Professional active = Samples.NewProfessional("Dra. Ana Prueba");
        Professional inactive = Samples.NewProfessional("Dr. Beto Prueba");
        inactive.Deactivate();
        CitasDbContext write = NewContext();
        write.Professionals.AddRange(active, inactive);
        await write.SaveChangesAsync();

        IReadOnlyList<Professional> listed = await new ProfessionalRepository(NewContext()).ListActiveAsync(CancellationToken.None);

        Professional only = Assert.Single(listed);
        Assert.Equal(active.Id, only.Id);
        Assert.Equal(7, only.WorkingHours.Count);
    }

    [Fact]
    public async Task ListActive_WithoutProfessionals_IsEmpty()
    {
        Assert.Empty(await new ProfessionalRepository(NewContext()).ListActiveAsync(CancellationToken.None));
    }
}
