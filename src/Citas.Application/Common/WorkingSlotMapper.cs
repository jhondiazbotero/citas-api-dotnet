using Citas.Domain.Common;
using Citas.Domain.ValueObjects;

namespace Citas.Application.Common;

internal static class WorkingSlotMapper
{
    /// <summary>Builds the domain slots; the first domain error stops the conversion.</summary>
    public static Result<List<WeeklySlot>> ToDomain(IEnumerable<WorkingSlotDto> slots)
    {
        List<WeeklySlot> result = [];
        foreach (WorkingSlotDto dto in slots)
        {
            Result<WeeklySlot> slot = WeeklySlot.Create(dto.Day, dto.From, dto.To);
            if (slot.IsFailure)
            {
                return slot.Error;
            }

            result.Add(slot.Value);
        }

        return result;
    }
}
