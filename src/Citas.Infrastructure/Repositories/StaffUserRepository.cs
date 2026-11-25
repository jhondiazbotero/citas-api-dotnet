using Citas.Application.Abstractions;
using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Repositories;

public sealed class StaffUserRepository(CitasDbContext db) : IStaffUserRepository
{
    public Task<StaffUser?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.StaffUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public void Add(StaffUser user) => db.StaffUsers.Add(user);
}
