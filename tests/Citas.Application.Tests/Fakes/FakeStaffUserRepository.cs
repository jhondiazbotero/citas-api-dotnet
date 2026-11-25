using Citas.Application.Abstractions;
using Citas.Domain.Staff;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeStaffUserRepository : IStaffUserRepository
{
    public List<StaffUser> Items { get; } = [];

    public List<string> Lookups { get; } = [];

    public Task<StaffUser?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        Lookups.Add(normalizedEmail);
        return Task.FromResult(Items.FirstOrDefault(u => u.Email == normalizedEmail));
    }

    public void Add(StaffUser user) => Items.Add(user);
}
