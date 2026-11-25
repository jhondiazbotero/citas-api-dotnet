using Citas.Application.Abstractions;
using Citas.Domain.Professionals;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Repositories;

public sealed class ProfessionalRepository(CitasDbContext db) : IProfessionalRepository
{
    public Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Professionals.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(Professional professional) => db.Professionals.Add(professional);
}
