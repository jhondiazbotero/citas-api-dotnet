using Citas.Domain.Patients;

namespace Citas.Application.Abstractions;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByDocumentAsync(string documentNumber, CancellationToken cancellationToken);

    void Add(Patient patient);
}
