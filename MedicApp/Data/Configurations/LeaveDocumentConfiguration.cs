using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public class LeaveDocumentConfiguration : IEntityTypeConfiguration<LeaveDocument>
{
    public void Configure(EntityTypeBuilder<LeaveDocument> builder)
    {
        builder.ToTable("SY_LeaveDocuments", "medic");
        builder.HasKey(x => x.Id).HasName("PK_SY_LeaveDocuments").IsClustered();
        builder.Property(x => x.Id).HasColumnName("PKLeaveDocument");
        builder.Property(x => x.MedicalLeaveId).HasColumnName("FKMedicalLeave");
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<MedicalLeave>().WithMany().HasForeignKey(x => x.MedicalLeaveId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.StorageKey).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(x => x.OriginalName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.MedicalLeaveId, x.RequestKey, x.Ordinal }).IsUnique();
        builder.HasIndex(x => new { x.MedicalLeaveId, x.LastUpdated });
        builder.ToTable(t => { t.HasCheckConstraint("CK_LeaveDocuments_Size", "[ByteCount] BETWEEN 1 AND 10485760"); t.HasCheckConstraint("CK_LeaveDocuments_Ordinal", "[Ordinal] BETWEEN 0 AND 4"); });
    }
}
