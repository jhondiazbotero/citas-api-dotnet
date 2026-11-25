using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Citas.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model and the migration scripts.
/// It never opens a connection, so the connection string carries no real credentials.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CitasDbContext>
{
    public CitasDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CitasDbContext>()
            .UseNpgsql("Host=localhost;Database=citas_design_time")
            .Options;
        return new CitasDbContext(options);
    }
}
