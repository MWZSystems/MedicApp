using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class MedicalLeaveConfiguration : IEntityTypeConfiguration<MedicalLeave>
{
    public void Configure(EntityTypeBuilder<MedicalLeave> builder)
    {
        builder.ToTable("SY_MedicalLeaves", "medic");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.Property(x => x.DoctorId).HasColumnName("FKDoctor");
        builder.Property(x => x.AppointmentId).HasColumnName("FKAppointment");
        builder.HasKey(x => x.Id).HasName("PK_SY_MedicalLeaves").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKMedicalLeave");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.StartsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.EndsAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SharedPerson>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PersonId, x.StartsAt, x.EndsAt });
        builder.Property(x => x.Reference).HasMaxLength(80);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.ToTable(t => { t.HasCheckConstraint("CK_Leaves_Dates", "[EndsAt] > [StartsAt]"); t.HasCheckConstraint("CK_Leaves_Status", "[Status] BETWEEN 1 AND 3"); t.HasCheckConstraint("CK_Leaves_Revision", "[Revision] > 0"); });
    }
}
