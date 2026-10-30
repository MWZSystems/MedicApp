using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class LaborEventConfiguration : IEntityTypeConfiguration<LaborEvent>
{
    public void Configure(EntityTypeBuilder<LaborEvent> builder)
    {
        builder.ToTable("IX_LaborEvents", "medic");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.Property(x => x.MedicalLeaveId).HasColumnName("FKMedicalLeave");
        builder.HasKey(x => x.Id).HasName("PK_IX_LaborEvents").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKLaborEvent");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.StartsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.EndsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<MedicalLeave>().WithMany().HasForeignKey(x => x.MedicalLeaveId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SharedPerson>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MedicalLeaveId, x.Revision }).IsUnique();
        builder.HasIndex(x => x.PersonId);
        builder.ToTable(t => { t.HasCheckConstraint("CK_LaborEvents_Dates", "[EndsAt] > [StartsAt]"); t.HasCheckConstraint("CK_LaborEvents_Status", "[Status] BETWEEN 1 AND 3"); });
    }
}
