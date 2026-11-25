using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Citas.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", table =>
        {
            table.HasCheckConstraint("ck_appointments_range", "end_at > start_at");
            table.HasCheckConstraint(
                "ck_appointments_status",
                "status IN ('Scheduled', 'Cancelled', 'Completed', 'NoShow')");
        });

        builder.HasKey(a => a.Id).HasName("pk_appointments");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ProfessionalId).HasColumnName("professional_id");
        builder.Property(a => a.PatientId).HasColumnName("patient_id");

        // TimeRange is a value object: two columns in the same table, no key of its own.
        builder.ComplexProperty(a => a.TimeRange, range =>
        {
            range.Property(r => r.Start).HasColumnName("start_at").HasColumnType("timestamp without time zone");
            range.Property(r => r.End).HasColumnName("end_at").HasColumnType("timestamp without time zone");
        });

        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(250);
        builder.Property(a => a.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(250);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(a => a.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp without time zone");

        // Optimistic concurrency with PostgreSQL's own row version (system column xmin), kept out of the domain class.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(a => a.ProfessionalId)
            .HasConstraintName("fk_appointments_professionals_professional_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .HasConstraintName("fk_appointments_patients_patient_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ProfessionalId).HasDatabaseName("ix_appointments_professional_id");
        builder.HasIndex(a => a.PatientId).HasDatabaseName("ix_appointments_patient_id");

        // Not expressible in the model, so the migration creates them with explicit SQL:
        // the two exclusion constraints (no overlapping bookings per professional or patient)
        // and the agenda index on (professional_id, start_at), because EF cannot index complex type properties.
    }
}
