using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Tests.Professionals;

public class ProfessionalTests
{
    // 2026-12-02 is a Wednesday.
    private static readonly DateTime Wednesday = new(2026, 12, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private static WeeklySlot Slot(DayOfWeek day, int fromHour, int toHour) =>
        WeeklySlot.Create(day, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0)).Value;

    private static TimeRange Range(int hour, int minute, int durationMinutes) =>
        TimeRange.Create(Wednesday.AddHours(hour).AddMinutes(minute), TimeSpan.FromMinutes(durationMinutes)).Value;

    private static Professional NewProfessional() => Professional.Create("Dr. Luis Mora", "Cardiology").Value;

    [Fact]
    public void Create_ValidData_StartsActiveWithoutWorkingHours()
    {
        var result = Professional.Create("Dr. Luis Mora", "Cardiology");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Dr. Luis Mora", result.Value.FullName);
        Assert.Equal("Cardiology", result.Value.Specialty);
        Assert.True(result.Value.IsActive);
        Assert.Empty(result.Value.WorkingHours);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData(" Luis")]
    [InlineData("Luis ")]
    public void Create_InvalidName_FailsWithInvalidName(string? name)
    {
        var result = Professional.Create(name!, "Cardiology");

        Assert.Equal(ProfessionalErrors.InvalidName, result.Error);
    }

    [Fact]
    public void Create_NameWith101Characters_FailsWithInvalidName()
    {
        var result = Professional.Create(new string('a', 101), "Cardiology");

        Assert.Equal(ProfessionalErrors.InvalidName, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C")]
    [InlineData("  ")]
    public void Create_InvalidSpecialty_FailsWithInvalidSpecialty(string? specialty)
    {
        var result = Professional.Create("Dr. Luis Mora", specialty!);

        Assert.Equal(ProfessionalErrors.InvalidSpecialty, result.Error);
    }

    [Theory]
    [InlineData(" Cardiology")]
    [InlineData("Cardiology ")]
    [InlineData("\tCardiology")]
    [InlineData("Cardiology\n")]
    public void Create_SpecialtyWithSurroundingSpace_FailsWithInvalidSpecialty(string specialty)
    {
        var result = Professional.Create("Dr. Luis Mora", specialty);

        Assert.Equal(ProfessionalErrors.InvalidSpecialty, result.Error);
    }

    [Fact]
    public void Create_SpecialtyWithAnInnerSpace_Succeeds()
    {
        Assert.True(Professional.Create("Dr. Luis Mora", "Medicina general").IsSuccess);
    }

    [Fact]
    public void Create_SpecialtyWith2Characters_Succeeds()
    {
        Assert.True(Professional.Create("Dr. Luis Mora", "ab").IsSuccess);
    }

    [Fact]
    public void Create_SpecialtyWith60Characters_Succeeds()
    {
        Assert.True(Professional.Create("Dr. Luis Mora", new string('a', 60)).IsSuccess);
    }

    [Fact]
    public void Create_SpecialtyWith61Characters_FailsWithInvalidSpecialty()
    {
        var result = Professional.Create("Dr. Luis Mora", new string('a', 61));

        Assert.Equal(ProfessionalErrors.InvalidSpecialty, result.Error);
    }

    [Fact]
    public void SetWorkingHours_ValidSlots_StoresThem()
    {
        var professional = NewProfessional();
        var slots = new[] { Slot(DayOfWeek.Monday, 9, 12), Slot(DayOfWeek.Monday, 14, 18), Slot(DayOfWeek.Tuesday, 9, 12) };

        var result = professional.SetWorkingHours(slots);

        Assert.True(result.IsSuccess);
        Assert.Equal(slots, professional.WorkingHours);
    }

    [Fact]
    public void SetWorkingHours_BackToBackSlotsSameDay_Succeeds()
    {
        var professional = NewProfessional();

        var result = professional.SetWorkingHours([Slot(DayOfWeek.Monday, 9, 12), Slot(DayOfWeek.Monday, 12, 14)]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetWorkingHours_EmptyList_FailsWithNoWorkingHours()
    {
        var professional = NewProfessional();

        var result = professional.SetWorkingHours([]);

        Assert.Equal(ProfessionalErrors.NoWorkingHours, result.Error);
    }

    [Fact]
    public void SetWorkingHours_OverlappingSlotsSameDay_FailsWithOverlappingWorkingHours()
    {
        var professional = NewProfessional();

        var result = professional.SetWorkingHours([Slot(DayOfWeek.Monday, 9, 12), Slot(DayOfWeek.Monday, 11, 14)]);

        Assert.Equal(ProfessionalErrors.OverlappingWorkingHours, result.Error);
    }

    [Fact]
    public void SetWorkingHours_SameHoursOnDifferentDays_Succeeds()
    {
        var professional = NewProfessional();

        var result = professional.SetWorkingHours([Slot(DayOfWeek.Monday, 9, 12), Slot(DayOfWeek.Tuesday, 9, 12)]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SetWorkingHours_Failure_KeepsThePreviousSlots()
    {
        var professional = NewProfessional();
        var original = new[] { Slot(DayOfWeek.Wednesday, 9, 12) };
        professional.SetWorkingHours(original);

        professional.SetWorkingHours([]);
        professional.SetWorkingHours([Slot(DayOfWeek.Monday, 9, 12), Slot(DayOfWeek.Monday, 11, 14)]);

        Assert.Equal(original, professional.WorkingHours);
    }

    [Fact]
    public void SetWorkingHours_Again_ReplacesThePreviousSlots()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Monday, 9, 12)]);
        var replacement = new[] { Slot(DayOfWeek.Friday, 8, 10) };

        professional.SetWorkingHours(replacement);

        Assert.Equal(replacement, professional.WorkingHours);
    }

    [Fact]
    public void Deactivate_ActiveProfessional_BecomesInactive()
    {
        var professional = NewProfessional();

        professional.Deactivate();

        Assert.False(professional.IsActive);
    }

    [Fact]
    public void IsAvailable_ActiveAndInsideASlot_IsTrue()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 14, 18)]);

        Assert.True(professional.IsAvailable(Range(14, 30, 30)));
    }

    [Fact]
    public void IsAvailable_RangeExactlyTheSlot_IsTrue()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 10)]);

        Assert.True(professional.IsAvailable(Range(9, 0, 60)));
    }

    [Fact]
    public void IsAvailable_RangeBetweenSlots_IsFalse()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 14, 18)]);

        Assert.False(professional.IsAvailable(Range(12, 0, 30)));
    }

    [Fact]
    public void IsAvailable_RangeSpanningTwoBackToBackSlots_IsTrue()
    {
        // 09:00-12:00 followed by 12:00-14:00 is one continuous stretch of work.
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 12, 14)]);

        Assert.True(professional.IsAvailable(Range(11, 30, 60)));
    }

    [Fact]
    public void IsAvailable_BackToBackSlotsStoredOutOfOrder_IsTrue()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 12, 14), Slot(DayOfWeek.Wednesday, 9, 12)]);

        Assert.True(professional.IsAvailable(Range(11, 30, 60)));
    }

    [Fact]
    public void IsAvailable_ThreeChainedSlots_IsTrue()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours(
            [Slot(DayOfWeek.Wednesday, 8, 10), Slot(DayOfWeek.Wednesday, 10, 12), Slot(DayOfWeek.Wednesday, 12, 14)]);

        Assert.True(professional.IsAvailable(Range(9, 0, 120)));
        Assert.True(professional.IsAvailable(Range(10, 30, 120)));
    }

    [Fact]
    public void IsAvailable_RangeStartsInsideAndEndsPastTheChain_IsFalse()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 12, 14)]);

        Assert.False(professional.IsAvailable(Range(13, 30, 60)));
    }

    [Fact]
    public void IsAvailable_SlotsWithAGapBetweenThem_IsFalseAcrossTheGap()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 13, 14)]);

        Assert.False(professional.IsAvailable(Range(11, 30, 120)));
    }

    [Fact]
    public void IsAvailable_BackToBackSlotsOnDifferentDays_IsFalseAcrossThem()
    {
        // Tuesday 09:00-12:00 and Wednesday 12:00-14:00 are not contiguous: the day is part of the slot.
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Tuesday, 9, 12), Slot(DayOfWeek.Wednesday, 12, 14)]);

        Assert.False(professional.IsAvailable(Range(11, 30, 60)));
    }

    [Fact]
    public void IsAvailable_ChainingDoesNotChangeTheStoredSlots()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12), Slot(DayOfWeek.Wednesday, 12, 14)]);

        professional.IsAvailable(Range(11, 30, 60));

        Assert.Equal(2, professional.WorkingHours.Count);
    }

    [Fact]
    public void IsAvailable_Inactive_IsFalse()
    {
        var professional = NewProfessional();
        professional.SetWorkingHours([Slot(DayOfWeek.Wednesday, 9, 12)]);
        professional.Deactivate();

        Assert.False(professional.IsAvailable(Range(9, 0, 30)));
    }

    [Fact]
    public void IsAvailable_WithoutWorkingHours_IsFalse()
    {
        Assert.False(NewProfessional().IsAvailable(Range(9, 0, 30)));
    }
}
