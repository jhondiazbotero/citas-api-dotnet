using Citas.Domain.Staff;

namespace Citas.Application.Abstractions;

/// <summary>A signed access token and the instant at which it stops being valid.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenIssuer
{
    AccessToken Issue(StaffUser user);
}
