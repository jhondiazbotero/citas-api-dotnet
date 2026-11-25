using Citas.Application.Abstractions;
using Citas.Domain.Common;
using Citas.Domain.Professionals;

namespace Citas.Application.Professionals;

/// <summary>What the public sees of a professional: enough to pick one and ask for free times. No hours, no internal data.</summary>
public sealed record ProfessionalSummaryResponse(Guid Id, string FullName, string Specialty);

/// <summary>The active professionals by name, for the public list that lets a visitor find an id.</summary>
public sealed class ListActiveProfessionalsHandler(IProfessionalRepository professionals)
{
    public async Task<Result<IReadOnlyList<ProfessionalSummaryResponse>>> HandleAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Professional> active = await professionals.ListActiveAsync(cancellationToken);

        // The repository already filters; checking again keeps this rule here, where it can be tested without a database.
        IReadOnlyList<ProfessionalSummaryResponse> result =
        [
            .. active
                .Where(p => p.IsActive)
                .OrderBy(p => p.FullName, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(p => p.Id)
                .Select(p => new ProfessionalSummaryResponse(p.Id, p.FullName, p.Specialty)),
        ];
        return Result.Success(result);
    }
}
