using Citas.Application.Common;
using Citas.Domain.Professionals;

namespace Citas.Application.Professionals;

public sealed record ProfessionalResponse(
    Guid Id,
    string FullName,
    string Specialty,
    bool IsActive,
    IReadOnlyList<WorkingSlotDto> WorkingHours)
{
    internal static ProfessionalResponse From(Professional professional) =>
        new(
            professional.Id,
            professional.FullName,
            professional.Specialty,
            professional.IsActive,
            [.. professional.WorkingHours.Select(s => new WorkingSlotDto(s.Day, s.From, s.To))]);
}
