using Citas.Application.Abstractions;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Repositories;
using Citas.Infrastructure.Security;
using Citas.Infrastructure.Seeding;
using Citas.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Citas.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Citas";

    public const string DefaultTimeZoneId = "America/Bogota";

    /// <summary>
    /// Registers the DbContext (connection string <c>ConnectionStrings:Citas</c>), the repositories, the password hasher, the unit of work,
    /// the clinic clock (<c>Clinic:TimeZone</c>, default America/Bogota) and the demo data seeder.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string 'ConnectionStrings:{ConnectionStringName}'.");

        string timeZoneId = configuration["Clinic:TimeZone"] is { Length: > 0 } configured ? configured : DefaultTimeZoneId;
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        services.AddDbContext<CitasDbContext>(options => options.UseNpgsql(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClinicClock>(sp => new ClinicClock(sp.GetRequiredService<TimeProvider>(), timeZone));

        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IStaffUserRepository, StaffUserRepository>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
