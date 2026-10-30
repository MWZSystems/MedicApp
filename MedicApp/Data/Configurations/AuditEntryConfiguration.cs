using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AR_AuditEntries", "medic");
        builder.HasKey(x => x.Id).HasName("PK_AR_AuditEntries").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKAuditEntry");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.EntityType).HasMaxLength(80);
        builder.Property(x => x.Operation).HasMaxLength(80);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.LastUpdated });
    }
}
