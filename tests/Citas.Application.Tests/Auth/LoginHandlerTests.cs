using Citas.Application.Auth;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;
using Citas.Domain.Staff;

namespace Citas.Application.Tests.Auth;

public class LoginHandlerTests
{
    private const string Password = "Una-clave-larga-1";

    private readonly FakeStaffUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenIssuer _tokens = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(new LoginValidator(), _users, _hasher, _tokens);
    }

    private StaffUser AddUser(string email = "admin@demo.local", StaffRole role = StaffRole.Admin, Guid? professionalId = null, bool active = true)
    {
        var user = StaffUser.Create(email, _hasher.Hash(Password), role, professionalId).Value;
        if (!active)
        {
            user.Deactivate();
        }

        _users.Add(user);
        return user;
    }

    private Task<Citas.Domain.Common.Result<LoginResponse>> LoginAsync(string email, string password) =>
        _handler.HandleAsync(new LoginCommand(email, password), CancellationToken.None);

    [Fact]
    public async Task ValidCredentials_ReturnTheTokenItsExpiryAndTheRole()
    {
        AddUser(role: StaffRole.Reception);

        var result = await LoginAsync("admin@demo.local", Password);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-for-admin@demo.local", result.Value.AccessToken);
        Assert.Equal(FakeTokenIssuer.Expiry, result.Value.ExpiresAt);
        Assert.Equal(StaffRole.Reception, result.Value.Role);
        Assert.Single(_tokens.Issued);
    }

    [Theory]
    [InlineData("ADMIN@Demo.Local")]
    [InlineData("  admin@demo.local ")]
    public async Task TheEmailIsNormalizedBeforeTheLookup(string typed)
    {
        AddUser();

        var result = await LoginAsync(typed, Password);

        Assert.True(result.IsSuccess);
        Assert.Equal(["admin@demo.local"], _users.Lookups);
    }

    [Fact]
    public async Task ProfessionalUser_GetsTheProfessionalRole()
    {
        AddUser("marquina@demo.local", StaffRole.Professional, Guid.NewGuid());

        var result = await LoginAsync("marquina@demo.local", Password);

        Assert.Equal(StaffRole.Professional, result.Value.Role);
    }

    [Fact]
    public async Task WrongPassword_IsInvalidCredentials_AndIssuesNothing()
    {
        AddUser();

        var result = await LoginAsync("admin@demo.local", "otra-clave");

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_tokens.Issued);
    }

    [Fact]
    public async Task UnknownEmail_IsInvalidCredentials()
    {
        AddUser();

        var result = await LoginAsync("nadie@demo.local", Password);

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_tokens.Issued);
    }

    [Fact]
    public async Task InactiveUser_WithTheRightPassword_IsInvalidCredentials()
    {
        AddUser(active: false);

        var result = await LoginAsync("admin@demo.local", Password);

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_tokens.Issued);
    }

    [Fact]
    public async Task EveryFailure_GivesTheSameErrorAndMessage()
    {
        AddUser();
        AddUser("baja@demo.local", active: false);

        var wrongPassword = await LoginAsync("admin@demo.local", "mala");
        var unknown = await LoginAsync("nadie@demo.local", Password);
        var inactive = await LoginAsync("baja@demo.local", Password);

        Assert.Equal(wrongPassword.Error, unknown.Error);
        Assert.Equal(wrongPassword.Error, inactive.Error);
    }

    [Fact]
    public async Task ThePasswordIsAlwaysChecked_EvenWhenTheUserDoesNotExistOrIsInactive()
    {
        AddUser();
        AddUser("baja@demo.local", active: false);

        await LoginAsync("nadie@demo.local", Password);
        await LoginAsync("baja@demo.local", Password);
        await LoginAsync("admin@demo.local", "mala");

        Assert.Equal(3, _hasher.VerifyCalls);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData("admin@demo.local", "")]
    [InlineData("admin@demo.local", "   ")]
    public async Task MissingEmailOrPassword_IsAValidationError(string email, string password)
    {
        AddUser();

        var result = await LoginAsync(email, password);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Empty(_users.Lookups);
    }

    [Fact]
    public async Task AnAbsurdlyLongPassword_IsRefusedBeforeHashing()
    {
        AddUser();

        var result = await LoginAsync("admin@demo.local", new string('x', LoginValidator.MaxPasswordLength + 1));

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task APasswordOfTheMaximumLength_IsChecked()
    {
        AddUser();

        var result = await LoginAsync("admin@demo.local", new string('x', LoginValidator.MaxPasswordLength));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Equal(1, _hasher.VerifyCalls);
    }
}
