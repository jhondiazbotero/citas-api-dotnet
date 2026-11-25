using Citas.Domain.Professionals;
using Citas.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Citas.Infrastructure.Persistence.Configurations;

internal sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.ToTable("staff_users", table =>
        {
            table.HasCheckConstraint("ck_staff_users_role", "role IN ('Admin', 'Reception', 'Professional')");

            // The same rule as the domain factory, enforced where nobody can skip it: only professionals are linked to a professional.
            table.HasCheckConstraint(
                "ck_staff_users_professional_link",
                "(role = 'Professional') = (professional_id IS NOT NULL)");
        });

        builder.HasKey(u => u.Id).HasName("pk_staff_users");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(StaffUser.MaxEmailLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.ProfessionalId).HasColumnName("professional_id");
        builder.Property(u => u.IsActive).HasColumnName("is_active");

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(u => u.ProfessionalId)
            .HasConstraintName("fk_staff_users_professionals_professional_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(ConstraintNames.StaffUserEmailUnique);
        builder.HasIndex(u => u.ProfessionalId).HasDatabaseName("ix_staff_users_professional_id");
    }
}
