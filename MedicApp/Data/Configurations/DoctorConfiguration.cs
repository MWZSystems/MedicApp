using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("CT_Doctors", "medic");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.HasKey(x => x.Id).HasName("PK_CT_Doctors").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKDoctor");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Specialty).HasMaxLength(120);
        builder.Property(x => x.ProfessionalLicense).HasMaxLength(60);
        builder.HasIndex(x => x.PersonId).IsUnique();
        builder.HasOne<SharedPerson>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}
