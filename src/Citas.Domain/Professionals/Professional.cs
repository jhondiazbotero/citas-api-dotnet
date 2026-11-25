using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Professionals;

public sealed class Professional
{
    private readonly List<WeeklySlot> _workingHours = [];

    private Professional(string fullName, string specialty)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        Specialty = specialty;
        IsActive = true;
    }

    public Guid Id { get; }

    public string FullName { get; }

    public string Specialty { get; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<WeeklySlot> WorkingHours => _workingHours;

    public static Result<Professional> Create(string fullName, string specialty)
    {
        if (!TextRules.IsTrimmedWithin(fullName, 2, 100))
        {
            return ProfessionalErrors.InvalidName;
        }

        if (string.IsNullOrWhiteSpace(specialty) || specialty.Length < 2 || specialty.Length > 60)
        {
            return ProfessionalErrors.InvalidSpecialty;
        }

        return new Professional(fullName, specialty);
    }

    /// <summary>Replaces the weekly schedule. On failure the previous schedule is kept.</summary>
    public Result SetWorkingHours(IEnumerable<WeeklySlot> slots)
    {
        List<WeeklySlot> candidate = [.. slots];
        if (candidate.Count == 0)
        {
            return Result.Failure(ProfessionalErrors.NoWorkingHours);
        }

        for (int i = 0; i < candidate.Count; i++)
        {
            for (int j = i + 1; j < candidate.Count; j++)
            {
                if (candidate[i].Overlaps(candidate[j]))
                {
                    return Result.Failure(ProfessionalErrors.OverlappingWorkingHours);
                }
            }
        }

        _workingHours.Clear();
        _workingHours.AddRange(candidate);
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    /// <summary>Active and the whole range fits inside one working slot.</summary>
    public bool IsAvailable(TimeRange range) => IsActive && _workingHours.Any(slot => slot.Contains(range));
}
