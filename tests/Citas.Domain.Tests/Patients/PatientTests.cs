using Citas.Domain.Errors;
using Citas.Domain.Patients;

namespace Citas.Domain.Tests.Patients;

public class PatientTests
{
    [Fact]
    public void Create_ValidData_Succeeds()
    {
        var result = Patient.Create("Ana Gomez", "1234567", "3001234567");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Ana Gomez", result.Value.FullName);
        Assert.Equal("1234567", result.Value.DocumentNumber);
        Assert.Equal("3001234567", result.Value.Phone);
    }

    [Fact]
    public void Create_WithoutPhone_Succeeds()
    {
        var result = Patient.Create("Ana Gomez", "1234567");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Phone);
    }

    [Fact]
    public void Create_TwoPatients_GetDifferentIds()
    {
        var a = Patient.Create("Ana Gomez", "1234567").Value;
        var b = Patient.Create("Ana Gomez", "1234567").Value;

        Assert.NotEqual(a.Id, b.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData(" Ana")]
    [InlineData("Ana ")]
    [InlineData("   ")]
    public void Create_InvalidName_FailsWithInvalidName(string? name)
    {
        var result = Patient.Create(name!, "1234567");

        Assert.Equal(PatientErrors.InvalidName, result.Error);
    }

    [Fact]
    public void Create_NameWithTwoCharacters_Succeeds()
    {
        Assert.True(Patient.Create("Al", "1234567").IsSuccess);
    }

    [Fact]
    public void Create_NameWith100Characters_Succeeds()
    {
        Assert.True(Patient.Create(new string('a', 100), "1234567").IsSuccess);
    }

    [Fact]
    public void Create_NameWith101Characters_FailsWithInvalidName()
    {
        var result = Patient.Create(new string('a', 101), "1234567");

        Assert.Equal(PatientErrors.InvalidName, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("12345678901234567890" + "1")]
    [InlineData("12345a")]
    [InlineData("123 456")]
    [InlineData("12.345")]
    [InlineData("١٢٣٤٥٦")]
    public void Create_InvalidDocument_FailsWithInvalidDocument(string? document)
    {
        var result = Patient.Create("Ana Gomez", document!);

        Assert.Equal(PatientErrors.InvalidDocument, result.Error);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678901234567890")]
    public void Create_DocumentOnTheLengthLimits_Succeeds(string document)
    {
        Assert.True(Patient.Create("Ana Gomez", document).IsSuccess);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    public void Create_PhoneOnTheLengthLimits_Succeeds(string phone)
    {
        Assert.True(Patient.Create("Ana Gomez", "1234567", phone).IsSuccess);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    [InlineData("300-123-4567")]
    [InlineData("+573001234567")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidPhone_FailsWithInvalidPhone(string phone)
    {
        var result = Patient.Create("Ana Gomez", "1234567", phone);

        Assert.Equal(PatientErrors.InvalidPhone, result.Error);
    }
}
