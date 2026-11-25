using Citas.Domain.Common;

namespace Citas.Domain.Errors;

/// <summary>Errors of signing in and of being allowed in. The messages are deliberately generic: they never say which part failed.</summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Email or password is incorrect.");

    public static readonly Error Unauthorized =
        new("Auth.Unauthorized", "Sign in with a valid access token to use this endpoint.");

    public static readonly Error Forbidden =
        new("Auth.Forbidden", "Your role does not allow this action.");

    public static readonly Error TooManyAttempts =
        new("Auth.TooManyAttempts", "Too many sign-in attempts. Wait before trying again.");
}
