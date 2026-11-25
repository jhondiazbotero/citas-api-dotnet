using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Persistence;

public sealed class CitasDbContext(DbContextOptions<CitasDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Professional> Professionals => Set<Professional>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CitasDbContext).Assembly);
}
