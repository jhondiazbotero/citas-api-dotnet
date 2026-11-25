using Citas.Domain.Staff;

namespace Citas.Application.Abstractions;

public interface IStaffUserRepository
{
    /// <summary>Looks the user up by an email already normalized with <see cref="StaffUser.NormalizeEmail"/>.</summary>
    Task<StaffUser?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    void Add(StaffUser user);
}
