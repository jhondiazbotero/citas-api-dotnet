using Citas.Application.Abstractions;
using Citas.Domain.Professionals;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeProfessionalRepository : IProfessionalRepository
{
    public List<Professional> Items { get; } = [];

    public Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public void Add(Professional professional) => Items.Add(professional);
}
