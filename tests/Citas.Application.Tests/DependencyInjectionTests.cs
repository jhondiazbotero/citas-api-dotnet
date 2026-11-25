using Citas.Application.Abstractions;
using Citas.Application.Appointments;
using Citas.Application.Auth;
using Citas.Application.Common;
using Citas.Application.Patients;
using Citas.Application.Professionals;
using Citas.Application.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Application.Tests;

public class DependencyInjectionTests
{
    private static readonly string[] ExpectedHandlers =
    [
        nameof(RegisterPatientHandler), nameof(GetPatientHandler),
        nameof(RegisterProfessionalHandler), nameof(SetWorkingHoursHandler),
        nameof(DeactivateProfessionalHandler), nameof(GetProfessionalHandler),
        nameof(ScheduleAppointmentHandler), nameof(RescheduleAppointmentHandler),
        nameof(CancelAppointmentHandler), nameof(CompleteAppointmentHandler), nameof(MarkNoShowHandler),
        nameof(GetAppointmentHandler), nameof(GetProfessionalAgendaHandler), nameof(GetAvailableSlotsHandler),
        nameof(LoginHandler),
    ];

    private static ServiceProvider BuildProvider(FakeClinicClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClinicClock>(clock ?? new FakeClinicClock(TestData.Now));
        services.AddSingleton<IProfessionalRepository, FakeProfessionalRepository>();
        services.AddSingleton<IPatientRepository, FakePatientRepository>();
        services.AddSingleton<IAppointmentRepository, FakeAppointmentRepository>();
        services.AddSingleton<IUnitOfWork, FakeUnitOfWork>();
        services.AddSingleton<IStaffUserRepository, FakeStaffUserRepository>();
        services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
        services.AddSingleton<ITokenIssuer, FakeTokenIssuer>();
        services.AddApplication();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void AddApplication_RegistersEveryHandlerOfTheAssembly()
    {
        var handlers = typeof(AssemblyReference).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Select(t => t.Name)
            .Order()
            .ToList();

        Assert.Equal(ExpectedHandlers.Order(), handlers);
    }

    [Fact]
    public void AddApplication_EveryHandlerCanBeResolved()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        foreach (Type handler in typeof(AssemblyReference).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("Handler", StringComparison.Ordinal)))
        {
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(handler));
        }
    }

    [Fact]
    public async Task AddApplication_FullFlowThroughTheContainer_RegistersAndSchedules()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var patient = await sp.GetRequiredService<RegisterPatientHandler>().HandleAsync(
            new RegisterPatientCommand("Ana Gomez", "1234567", null), CancellationToken.None);
        var professional = await sp.GetRequiredService<RegisterProfessionalHandler>().HandleAsync(
            new RegisterProfessionalCommand("Dr. Luis Mora", "Cardiology", [TestData.WednesdayMorning]), CancellationToken.None);
        var appointment = await sp.GetRequiredService<ScheduleAppointmentHandler>().HandleAsync(
            new ScheduleAppointmentCommand(professional.Value.Id, patient.Value.Id, TestData.At(2, 10, 0), 30, null),
            CancellationToken.None);
        var agenda = await sp.GetRequiredService<GetProfessionalAgendaHandler>().HandleAsync(
            new GetProfessionalAgendaQuery(professional.Value.Id, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 2)),
            CancellationToken.None);

        Assert.True(appointment.IsSuccess);
        Assert.Equal(appointment.Value.Id, Assert.Single(agenda.Value).Id);
    }

    [Fact]
    public void AddApplication_ValidatorsAreRegisteredForEveryCommandAndQuery()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetService<FluentValidation.IValidator<ScheduleAppointmentCommand>>());
        Assert.NotNull(provider.GetService<FluentValidation.IValidator<GetAvailableSlotsQuery>>());
        Assert.NotNull(provider.GetService<FluentValidation.IValidator<SetWorkingHoursCommand>>());
    }
}
