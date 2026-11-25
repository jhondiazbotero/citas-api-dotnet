using Citas.Domain.Professionals;

namespace Citas.Application.Abstractions;

public interface IProfessionalRepository
{
    Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Professional professional);
}
