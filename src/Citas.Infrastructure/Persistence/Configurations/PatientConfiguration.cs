using Citas.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Citas.Infrastructure.Persistence.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");
        builder.HasKey(p => p.Id).HasName("pk_patients");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.DocumentNumber).HasColumnName("document_number").HasMaxLength(20).IsRequired();
        builder.Property(p => p.Phone).HasColumnName("phone").HasMaxLength(15);

        builder.HasIndex(p => p.DocumentNumber).IsUnique().HasDatabaseName(ConstraintNames.PatientDocumentUnique);
    }
}
