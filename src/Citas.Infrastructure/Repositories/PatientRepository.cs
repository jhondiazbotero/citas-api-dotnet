using Citas.Application.Abstractions;
using Citas.Domain.Patients;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Repositories;

public sealed class PatientRepository(CitasDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsByDocumentAsync(string documentNumber, CancellationToken cancellationToken) =>
        db.Patients.AnyAsync(p => p.DocumentNumber == documentNumber, cancellationToken);

    public void Add(Patient patient) => db.Patients.Add(patient);
}
