using Citas.Application.StaffUsers;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.Staff;

namespace Citas.Application.Tests.StaffUsers;

public class CreateStaffUserHandlerTests
{
    private const string Password = "Una-clave-larga-1";

    private readonly FakeStaffUserRepository _users = new();
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task<Result<StaffUserResponse>> CreateAsync(
        string email = "Recepcion@Clinica.Test",
        string password = Password,
        StaffRole role = StaffRole.Reception,
        Guid? professionalId = null) =>
        new CreateStaffUserHandler(new CreateStaffUserValidator(), _users, _professionals, _hasher, _unitOfWork)
            .HandleAsync(new CreateStaffUserCommand(email, password, role, professionalId), CancellationToken.None);

    private Professional AddProfessional(bool active = true)
    {
        var professional = TestData.NewProfessional();
        if (!active)
        {
            professional.Deactivate();
        }

        _professionals.Items.Add(professional);
        return professional;
    }

    [Fact]
    public async Task Reception_IsCreatedWithAHashedPasswordAndANormalizedEmail()
    {
        var result = await CreateAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var saved = Assert.Single(_users.Items);
        Assert.Equal("recepcion@clinica.test", saved.Email);
        Assert.Equal(StaffRole.Reception, saved.Role);
        Assert.Equal(_hasher.Hash(Password), saved.PasswordHash);
        Assert.NotEqual(Password, saved.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCalls);
        Assert.Equal(new StaffUserResponse(saved.Id, "recepcion@clinica.test", StaffRole.Reception, null, true), result.Value);
    }

    [Fact]
    public async Task Professional_IsLinkedToAnExistingActiveProfessional()
    {
        var professional = AddProfessional();

        var result = await CreateAsync("doctor@clinica.test", role: StaffRole.Professional, professionalId: professional.Id);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(professional.Id, Assert.Single(_users.Items).ProfessionalId);
        Assert.Equal(professional.Id, result.Value.ProfessionalId);
    }

    [Fact]
    public async Task TheResponse_HasNoPasswordAndNoHash()
    {
        string[] properties = [.. typeof(StaffUserResponse).GetProperties().Select(p => p.Name).Order()];

        Assert.Equal(["Email", "Id", "IsActive", "ProfessionalId", "Role"], properties);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Admin_CannotBeCreatedThroughTheUseCase()
    {
        var result = await CreateAsync(role: StaffRole.Admin);

        Assert.Equal(StaffUserErrors.RoleNotAllowed, result.Error);
        Assert.Empty(_users.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task UnknownRole_IsRefused()
    {
        var result = await CreateAsync(role: (StaffRole)99);

        Assert.Equal(StaffUserErrors.RoleNotAllowed, result.Error);
    }

    [Fact]
    public async Task Professional_WithoutAProfessionalId_FailsWithProfessionalRequired()
    {
        var result = await CreateAsync(role: StaffRole.Professional);

        Assert.Equal(StaffUserErrors.ProfessionalRequired, result.Error);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Professional_ThatDoesNotExist_FailsWithNotFound()
    {
        var result = await CreateAsync(role: StaffRole.Professional, professionalId: Guid.NewGuid());

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Professional_ThatIsInactive_FailsWithInactive()
    {
        var professional = AddProfessional(active: false);

        var result = await CreateAsync(role: StaffRole.Professional, professionalId: professional.Id);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Reception_WithAProfessionalId_FailsWithProfessionalNotAllowed()
    {
        var professional = AddProfessional();

        var result = await CreateAsync(role: StaffRole.Reception, professionalId: professional.Id);

        Assert.Equal(StaffUserErrors.ProfessionalNotAllowed, result.Error);
    }

    [Fact]
    public async Task RepeatedEmail_FailsWithDuplicateEmail_EvenWithDifferentCase()
    {
        await CreateAsync("recepcion@clinica.test");

        var result = await CreateAsync(" RECEPCION@clinica.test ");

        Assert.Equal(StaffUserErrors.DuplicateEmail, result.Error);
        Assert.Single(_users.Items);
    }

    [Fact]
    public async Task WhenTheStoreRejectsTheEmail_ThatErrorComesBack()
    {
        // Two requests with the same email can both pass the lookup; the unique index of the database is the last word.
        _unitOfWork.FailWith = StaffUserErrors.DuplicateEmail;

        var result = await CreateAsync();

        Assert.Equal(StaffUserErrors.DuplicateEmail, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("dos@@arrobas.test")]
    [InlineData("sin@punto")]
    public async Task InvalidEmail_IsRefused(string email)
    {
        var result = await CreateAsync(email);

        Assert.True(result.IsFailure);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task InvalidEmail_FailsWithInvalidEmail()
    {
        Assert.Equal(StaffUserErrors.InvalidEmail, (await CreateAsync("sin-arroba")).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta-11-ch")]
    public async Task PasswordShorterThan12_IsRefused_WithAValidationError(string password)
    {
        var result = await CreateAsync(password: password);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Password_OfExactly12Characters_IsAccepted()
    {
        Assert.True((await CreateAsync(password: new string('a', 12))).IsSuccess);
    }

    [Fact]
    public async Task Password_OfExactly256Characters_IsAccepted_AndOf257IsNot()
    {
        Assert.True((await CreateAsync("uno@clinica.test", new string('a', 256))).IsSuccess);

        var tooLong = await CreateAsync("dos@clinica.test", new string('a', 257));

        Assert.Equal("Validation", tooLong.Error.Code);
        Assert.Single(_users.Items);
    }

    [Fact]
    public async Task APasswordThatIsOnlySpaces_IsRefused()
    {
        // Twelve spaces pass the length rule but are not a password.
        var result = await CreateAsync(password: new string(' ', 12));

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task TheErrorOfARefusedPassword_NeverRepeatsIt()
    {
        var result = await CreateAsync(password: "corta");

        Assert.DoesNotContain("corta", result.Error.Message, StringComparison.Ordinal);
    }
}
