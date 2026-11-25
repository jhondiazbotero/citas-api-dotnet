using Citas.Domain.Professionals;

namespace Citas.Application.Abstractions;

public interface IProfessionalRepository
{
    Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The professionals who are still active, in no particular order.</summary>
    Task<IReadOnlyList<Professional>> ListActiveAsync(CancellationToken cancellationToken);

    void Add(Professional professional);
}
