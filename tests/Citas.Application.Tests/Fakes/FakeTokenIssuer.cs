using Citas.Application.Abstractions;
using Citas.Domain.Staff;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeTokenIssuer : ITokenIssuer
{
    public static readonly DateTimeOffset Expiry = new(2026, 12, 1, 14, 0, 0, TimeSpan.Zero);

    public List<StaffUser> Issued { get; } = [];

    public AccessToken Issue(StaffUser user)
    {
        Issued.Add(user);
        return new AccessToken($"token-for-{user.Email}", Expiry);
    }
}
