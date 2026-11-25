using Citas.Application.Abstractions;
using Citas.Domain.Patients;

namespace Citas.Application.Tests.Fakes;

public sealed class FakePatientRepository : IPatientRepository
{
    public List<Patient> Items { get; } = [];

    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<bool> ExistsByDocumentAsync(string documentNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(p => p.DocumentNumber == documentNumber));

    public void Add(Patient patient) => Items.Add(patient);
}
