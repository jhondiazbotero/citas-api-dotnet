using Citas.Domain.Professionals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Citas.Infrastructure.Persistence.Configurations;

internal sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        builder.ToTable("professionals");
        builder.HasKey(p => p.Id).HasName("pk_professionals");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Specialty).HasColumnName("specialty").HasMaxLength(60).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active");

        // The weekly schedule is a collection of value objects that lives in its own table.
        builder.OwnsMany(p => p.WorkingHours, slot =>
        {
            slot.ToTable(
                "professional_working_hours",
                table => table.HasCheckConstraint("ck_professional_working_hours_range", "from_time < to_time"));

            slot.WithOwner().HasForeignKey("professional_id");
            slot.Property<int>("id").ValueGeneratedOnAdd();
            slot.HasKey("id").HasName("pk_professional_working_hours");

            slot.Property(s => s.Day).HasColumnName("day_of_week");
            slot.Property(s => s.From).HasColumnName("from_time");
            slot.Property(s => s.To).HasColumnName("to_time");

            slot.HasIndex("professional_id").HasDatabaseName("ix_professional_working_hours_professional_id");
            slot.WithOwner().HasConstraintName("fk_professional_working_hours_professionals_professional_id");
        });

        builder.Navigation(p => p.WorkingHours).HasField("_workingHours").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
