using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Staff;
using FluentValidation;

namespace Citas.Application.Auth;

public sealed record LoginCommand(string Email, string Password);

/// <param name="AccessToken">Bearer token to send in the Authorization header.</param>
/// <param name="ExpiresAt">The instant (UTC) at which the token stops being valid.</param>
/// <param name="Role">Role of the user, for the client to adapt its screens. The server decides access from the token.</param>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, StaffRole Role);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Longer passwords are refused before hashing: hashing a huge input would be a cheap way to burn CPU.</summary>
    public const int MaxPasswordLength = 256;

    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Password is required.");
        RuleFor(c => c.Password).MaximumLength(MaxPasswordLength).WithMessage($"Password is at most {MaxPasswordLength} characters.");
    }
}

/// <summary>
/// Signs a staff member in. Every way of failing (unknown email, wrong password, inactive user) gives the same error,
/// and the password is always checked, even for an unknown email, so neither the answer nor the time says which one it was.
/// </summary>
public sealed class LoginHandler(
    IValidator<LoginCommand> validator,
    IStaffUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens)
{
    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        StaffUser? user = await users.GetByEmailAsync(StaffUser.NormalizeEmail(command.Email), cancellationToken);

        // Always verify, so a missing user costs as much time as a wrong password.
        bool passwordMatches = hasher.Verify(user?.PasswordHash, command.Password);
        if (user is null || !user.IsActive || !passwordMatches)
        {
            return AuthErrors.InvalidCredentials;
        }

        AccessToken token = tokens.Issue(user);
        return new LoginResponse(token.Value, token.ExpiresAt, user.Role);
    }
}
