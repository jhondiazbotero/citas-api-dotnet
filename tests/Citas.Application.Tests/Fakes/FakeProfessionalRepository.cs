using Citas.Application.Abstractions;
using Citas.Domain.Professionals;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeProfessionalRepository : IProfessionalRepository
{
    public List<Professional> Items { get; } = [];

    public Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    /// <summary>Returns everything it holds, on purpose: the handler must not rely on the repository for the active filter alone.</summary>
    public Task<IReadOnlyList<Professional>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Professional>>([.. Items]);

    public void Add(Professional professional) => Items.Add(professional);
}
