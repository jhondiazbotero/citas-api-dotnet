using Citas.Application.Abstractions;
using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.Staff;
using Citas.Domain.ValueObjects;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Citas.Infrastructure.Seeding;

/// <summary>
/// Loads a small, entirely fictional data set so the API has something to show. Safe to run many times:
/// people are matched by document number or full name, and appointments are only created for patients without any.
/// Sign-in users for the demo (admin, reception, one per professional) are created only when <c>Demo:UsersPassword</c> is set:
/// the password never lives in a versioned file.
/// </summary>
public sealed class DemoDataSeeder(
    CitasDbContext db,
    IClinicClock clock,
    IConfiguration configuration,
    IPasswordHasher passwordHasher,
    ILogger<DemoDataSeeder> logger)
{
    public const string ConfigurationKey = "Demo:SeedData";

    public const string UsersPasswordKey = "Demo:UsersPassword";

    public const string AdminEmail = "admin@demo.local";

    public const string ReceptionEmail = "recepcion@demo.local";

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static readonly (string Name, string Specialty, bool WorksSaturday, string Email)[] ProfessionalData =
    [
        ("Dra. Elena Marquina", "Medicina general", true, "marquina@demo.local"),
        ("Dr. Tomás Alderete", "Odontología", false, "alderete@demo.local"),
        ("Dra. Irene Valcárcel", "Psicología", false, "valcarcel@demo.local"),
    ];

    private static readonly (string Name, string Document, string Phone)[] PatientData =
    [
        ("Lucía Ortega Pardo", "90000001", "3000000101"),
        ("Mateo Lagos Rivas", "90000002", "3000000102"),
        ("Valentina Roa Durán", "90000003", "3000000103"),
        ("Samuel Cifuentes Mena", "90000004", "3000000104"),
        ("Paula Nieto Salgado", "90000005", "3000000105"),
    ];

    // (professional index, patient index, days ahead, hour, minute, minutes). Days ahead are counted in business days.
    private static readonly (int Professional, int Patient, int BusinessDays, int Hour, int Minute, int Minutes, string Reason)[] AppointmentData =
    [
        (0, 0, 2, 9, 0, 30, "Control general"),
        (0, 1, 2, 9, 30, 30, "Dolor de cabeza frecuente"),
        (1, 2, 3, 10, 0, 60, "Limpieza dental"),
        (1, 3, 4, 15, 0, 45, "Revisión de ortodoncia"),
        (2, 4, 3, 16, 0, 60, "Primera consulta"),
        (2, 0, 5, 8, 30, 60, "Seguimiento"),
    ];

    /// <summary>Runs <see cref="SeedAsync"/> only when <c>Demo:SeedData</c> is true. Returns whether it ran.</summary>
    public async Task<bool> SeedIfEnabledAsync(CancellationToken cancellationToken)
    {
        if (!bool.TryParse(configuration[ConfigurationKey], out bool enabled) || !enabled)
        {
            return false;
        }

        await SeedAsync(cancellationToken);
        return true;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        List<Professional> professionals = await EnsureProfessionalsAsync(cancellationToken);
        List<Patient> patients = await EnsurePatientsAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await EnsureAppointmentsAsync(professionals, patients, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await EnsureUsersAsync(professionals, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUsersAsync(List<Professional> professionals, CancellationToken cancellationToken)
    {
        string? password = configuration[UsersPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Demo users were not created because {Setting} is not set. Set it (user-secrets or an environment variable) to be able to sign in",
                UsersPasswordKey);
            return;
        }

        List<(string Email, StaffRole Role, Guid? ProfessionalId)> wanted =
        [
            (AdminEmail, StaffRole.Admin, null),
            (ReceptionEmail, StaffRole.Reception, null),
        ];
        for (int i = 0; i < ProfessionalData.Length; i++)
        {
            wanted.Add((ProfessionalData[i].Email, StaffRole.Professional, professionals[i].Id));
        }

        HashSet<string> existing = [.. await db.StaffUsers.Select(u => u.Email).ToListAsync(cancellationToken)];
        foreach ((string email, StaffRole role, Guid? professionalId) in wanted.Where(w => !existing.Contains(w.Email)))
        {
            // One salt per user: the hasher makes a new one on every call.
            db.StaffUsers.Add(StaffUser.Create(email, passwordHasher.Hash(password), role, professionalId).Value);
        }
    }

    private async Task<List<Professional>> EnsureProfessionalsAsync(CancellationToken cancellationToken)
    {
        List<Professional> result = [];
        foreach ((string name, string specialty, bool worksSaturday, _) in ProfessionalData)
        {
            Professional? existing = await db.Professionals.FirstOrDefaultAsync(p => p.FullName == name, cancellationToken);
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }

            Professional professional = Professional.Create(name, specialty).Value;
            professional.SetWorkingHours(BuildWorkingHours(worksSaturday));
            db.Professionals.Add(professional);
            result.Add(professional);
        }

        return result;
    }

    private async Task<List<Patient>> EnsurePatientsAsync(CancellationToken cancellationToken)
    {
        List<Patient> result = [];
        foreach ((string name, string document, string phone) in PatientData)
        {
            Patient? existing = await db.Patients.FirstOrDefaultAsync(p => p.DocumentNumber == document, cancellationToken);
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }

            Patient patient = Patient.Create(name, document, phone).Value;
            db.Patients.Add(patient);
            result.Add(patient);
        }

        return result;
    }

    private async Task EnsureAppointmentsAsync(
        List<Professional> professionals,
        List<Patient> patients,
        CancellationToken cancellationToken)
    {
        DateTime now = clock.Now;
        HashSet<Guid> patientsWithAppointments = [.. await db.Appointments
            .Where(a => patients.Select(p => p.Id).Contains(a.PatientId))
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync(cancellationToken)];

        foreach (var data in AppointmentData)
        {
            Professional professional = professionals[data.Professional];
            Patient patient = patients[data.Patient];
            if (patientsWithAppointments.Contains(patient.Id))
            {
                continue;
            }

            DateTime start = AddBusinessDays(now.Date, data.BusinessDays).AddHours(data.Hour).AddMinutes(data.Minute);
            var range = TimeRange.Create(start, TimeSpan.FromMinutes(data.Minutes));
            if (range.IsFailure)
            {
                continue;
            }

            // Look at what is already booked for this professional or patient (saved or pending) so the demo never collides.
            List<Appointment> existing = await db.Appointments
                .Where(a => a.ProfessionalId == professional.Id || a.PatientId == patient.Id)
                .ToListAsync(cancellationToken);
            existing.AddRange(db.ChangeTracker.Entries<Appointment>().Select(e => e.Entity).Except(existing));

            var appointment = AppointmentScheduler.Schedule(professional, patient, range.Value, data.Reason, existing, now);
            if (appointment.IsSuccess)
            {
                db.Appointments.Add(appointment.Value);
            }
        }
    }

    private static List<WeeklySlot> BuildWorkingHours(bool worksSaturday)
    {
        List<WeeklySlot> slots = [];
        foreach (DayOfWeek day in Weekdays)
        {
            slots.Add(WeeklySlot.Create(day, new TimeOnly(8, 0), new TimeOnly(12, 0)).Value);
            slots.Add(WeeklySlot.Create(day, new TimeOnly(14, 0), new TimeOnly(18, 0)).Value);
        }

        if (worksSaturday)
        {
            slots.Add(WeeklySlot.Create(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0)).Value);
        }

        return slots;
    }

    private static DateTime AddBusinessDays(DateTime from, int days)
    {
        DateTime date = from;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days--;
            }
        }

        return date;
    }
}
