using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class AccessProfileConfiguration : IEntityTypeConfiguration<AccessProfile>
{
    public void Configure(EntityTypeBuilder<AccessProfile> builder)
    {
        builder.ToTable("SC_AccessProfiles", "medic");
        builder.HasKey(x => x.Id).HasName("PK_SC_AccessProfiles").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKAccessProfile");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.UserId).HasColumnName("FKUser").HasMaxLength(450);
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
