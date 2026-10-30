using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("SY_Appointments", "medic");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.Property(x => x.DoctorId).HasColumnName("FKDoctor");
        builder.Property(x => x.AvailabilityWindowId).HasColumnName("FKAvailabilityWindow");
        builder.HasKey(x => x.Id).HasName("PK_SY_Appointments").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKAppointment");
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
        builder.HasOne<AvailabilityWindow>().WithMany().HasForeignKey(x => x.AvailabilityWindowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DoctorId, x.StartsAt, x.EndsAt });
        builder.HasIndex(x => new { x.PersonId, x.StartsAt, x.EndsAt });
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.ToTable(t => { t.HasCheckConstraint("CK_Appointments_Dates", "[EndsAt] > [StartsAt]"); t.HasCheckConstraint("CK_Appointments_Status", "[Status] BETWEEN 1 AND 4"); });
    }
}
