using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class AvailabilityWindowConfiguration : IEntityTypeConfiguration<AvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<AvailabilityWindow> builder)
    {
        builder.ToTable("SY_AvailabilityWindows", "medic");
        builder.Property(x => x.DoctorId).HasColumnName("FKDoctor");
        builder.HasKey(x => x.Id).HasName("PK_SY_AvailabilityWindows").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKAvailabilityWindow");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.StartsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.EndsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DoctorId, x.StartsAt, x.EndsAt });
        builder.ToTable(t => { t.HasCheckConstraint("CK_Windows_Dates", "[EndsAt] > [StartsAt]"); t.HasCheckConstraint("CK_Windows_Slot", "[SlotMinutes] BETWEEN 5 AND 120"); });
    }
}
