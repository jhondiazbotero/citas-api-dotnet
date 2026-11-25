using Citas.Application.Patients;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Patients;

public class RegisterPatientHandlerTests
{
    private readonly FakePatientRepository _patients = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private RegisterPatientHandler CreateHandler() =>
        new(new RegisterPatientValidator(), _patients, _unitOfWork);

    [Fact]
    public async Task HandleAsync_ValidData_AddsPatientAndSaves()
    {
        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand("Ana Gomez", "1234567", "3001234567"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana Gomez", result.Value.FullName);
        Assert.Equal("1234567", result.Value.DocumentNumber);
        Assert.Equal("3001234567", result.Value.Phone);
        Assert.Single(_patients.Items);
        Assert.Equal(_patients.Items[0].Id, result.Value.Id);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_DocumentAlreadyRegistered_FailsWithDuplicateDocumentAndDoesNotSave()
    {
        _patients.Items.Add(TestData.NewPatient("1234567"));

        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand("Luisa Perez", "1234567", null), CancellationToken.None);

        Assert.Equal(PatientErrors.DuplicateDocument, result.Error);
        Assert.Single(_patients.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_DomainRejectsDocument_PropagatesErrorWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand("Ana Gomez", "12AB", null), CancellationToken.None);

        Assert.Equal(PatientErrors.InvalidDocument, result.Error);
        Assert.Empty(_patients.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NullName_FailsWithValidationErrorWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand(null!, "1234567", null), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Empty(_patients.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NullDocument_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand("Ana Gomez", null!, null), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_PropagatesTheStoreError()
    {
        _unitOfWork.FailWith = PatientErrors.DuplicateDocument;

        var result = await CreateHandler().HandleAsync(
            new RegisterPatientCommand("Ana Gomez", "1234567", null), CancellationToken.None);

        Assert.Equal(PatientErrors.DuplicateDocument, result.Error);
    }
}
