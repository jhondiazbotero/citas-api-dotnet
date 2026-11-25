namespace Citas.Domain.Staff;

/// <summary>What a member of the clinic's staff may do. Patients do not sign in to this API.</summary>
public enum StaffRole
{
    /// <summary>Manages professionals and also does everything the front desk does.</summary>
    Admin = 1,

    /// <summary>Front desk: patients and appointments.</summary>
    Reception = 2,

    /// <summary>A professional of the clinic: sees their own agenda and closes their own appointments.</summary>
    Professional = 3,
}
