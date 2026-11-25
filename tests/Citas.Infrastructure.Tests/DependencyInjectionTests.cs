using Citas.Application.Abstractions;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        // A real host registers IConfiguration itself; the test host has to do it by hand.
        return new ServiceCollection()
            .AddSingleton(configuration)
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
    }

    private static readonly Dictionary<string, string?> WithConnection = new()
    {
        ["ConnectionStrings:Citas"] = "Host=localhost;Database=ignored",
    };

    [Fact]
    public void AddInfrastructure_RegistersEverythingTheApplicationNeeds()
    {
        using ServiceProvider provider = Build(WithConnection);
        using IServiceScope scope = provider.CreateScope();

        Assert.IsType<CitasDbContext>(scope.ServiceProvider.GetRequiredService<CitasDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPatientRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProfessionalRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAppointmentRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IClinicClock>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DemoDataSeeder>());
    }

    [Fact]
    public void AddInfrastructure_WithoutConnectionString_FailsWithAClearMessage()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build([]));

        Assert.Contains("ConnectionStrings:Citas", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_UnknownTimeZone_FailsAtStartup()
    {
        var values = new Dictionary<string, string?>(WithConnection) { ["Clinic:TimeZone"] = "Mars/Olympus" };

        Assert.Throws<TimeZoneNotFoundException>(() => Build(values));
    }

    [Fact]
    public void AddInfrastructure_ClockIsInTheConfiguredZone_AndDefaultsToBogota()
    {
        using ServiceProvider defaults = Build(WithConnection);
        var tokyoValues = new Dictionary<string, string?>(WithConnection) { ["Clinic:TimeZone"] = "Asia/Tokyo" };
        using ServiceProvider tokyo = Build(tokyoValues);

        TimeSpan difference = tokyo.GetRequiredService<IClinicClock>().Now - defaults.GetRequiredService<IClinicClock>().Now;

        // Tokyo (UTC+9) is 14 hours ahead of Bogota (UTC-5); allow a few seconds between the two reads.
        Assert.InRange(difference, TimeSpan.FromHours(14) - TimeSpan.FromSeconds(5), TimeSpan.FromHours(14) + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddInfrastructure_ClockAndRepositoriesHaveTheRightLifetimes()
    {
        using ServiceProvider provider = Build(WithConnection);

        using (IServiceScope first = provider.CreateScope())
        using (IServiceScope second = provider.CreateScope())
        {
            Assert.NotSame(
                first.ServiceProvider.GetRequiredService<IAppointmentRepository>(),
                second.ServiceProvider.GetRequiredService<IAppointmentRepository>());
            Assert.Same(
                first.ServiceProvider.GetRequiredService<IClinicClock>(),
                second.ServiceProvider.GetRequiredService<IClinicClock>());
        }
    }
}
