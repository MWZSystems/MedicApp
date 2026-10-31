using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MedicApp.Data.Configurations;

public static class ClinicalRecordMapping
{
    public static void Base<T>(EntityTypeBuilder<T> builder, string table, string key) where T : MedicalRecord
    {
        builder.ToTable(table, "medic");
        builder.HasKey(x => x.Id).HasName("PK_" + table).IsClustered();
        builder.Property(x => x.Id).HasColumnName(key);
        builder.Property(x => x.UpdatedById).HasColumnName("FKUserUpdater").HasMaxLength(450).IsRequired();
        builder.HasOne<SharedUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UpdatedById);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.LastUpdated).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
public class ClinicalVisitConfiguration : IEntityTypeConfiguration<ClinicalVisit>
{
    public void Configure(EntityTypeBuilder<ClinicalVisit> builder)
    {
        ClinicalRecordMapping.Base(builder, "SY_ClinicalVisits", "PKClinicalVisit");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.HasOne<SharedPerson>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.VisitedAt).HasColumnType("datetime2(0)");
        builder.HasIndex(x => new { x.PersonId, x.VisitedAt });
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Diagnosis).HasMaxLength(2000).IsRequired();
        foreach (var field in new[] { "Findings", "Treatment", "Prescription", "Notes" }) { builder.Property<string>(field).HasMaxLength(4000).IsRequired(); }
        foreach (var field in new[] { "Weight", "Height", "Temperature", "Glucose" }) { builder.Property<decimal?>(field).HasPrecision(7, 2); }
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ClinicalVisits_Vitals", "([Weight] IS NULL OR [Weight] BETWEEN 0.1 AND 700) AND ([Height] IS NULL OR [Height] BETWEEN 1 AND 300) AND ([Temperature] IS NULL OR [Temperature] BETWEEN 20 AND 50) AND ([Systolic] IS NULL OR [Systolic] BETWEEN 20 AND 300) AND ([Diastolic] IS NULL OR [Diastolic] BETWEEN 10 AND 200) AND ([Pulse] IS NULL OR [Pulse] BETWEEN 1 AND 300) AND ([RespiratoryRate] IS NULL OR [RespiratoryRate] BETWEEN 1 AND 150) AND ([OxygenSaturation] IS NULL OR [OxygenSaturation] BETWEEN 1 AND 100) AND ([Glucose] IS NULL OR [Glucose] BETWEEN 1 AND 1500)");
        });
    }
}
public class PatientDocumentConfiguration : IEntityTypeConfiguration<PatientDocument>
{
    public void Configure(EntityTypeBuilder<PatientDocument> builder)
    {
        ClinicalRecordMapping.Base(builder, "SY_PatientDocuments", "PKPatientDocument");
        builder.Property(x => x.PersonId).HasColumnName("FKPerson");
        builder.Property(x => x.ClinicalVisitId).HasColumnName("FKClinicalVisit");
        builder.HasOne<SharedPerson>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClinicalVisit>().WithMany().HasForeignKey(x => x.ClinicalVisitId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(x => x.OriginalName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.PersonId, x.RequestKey, x.Ordinal }).IsUnique();
        builder.HasIndex(x => new { x.PersonId, x.LastUpdated });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PatientDocuments_Size", "[ByteCount] BETWEEN 1 AND 10485760");
            t.HasCheckConstraint("CK_PatientDocuments_Ordinal", "[Ordinal] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_PatientDocuments_Kind", "[Kind] BETWEEN 0 AND 4");
        });
    }
}
public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        ClinicalRecordMapping.Base(builder, "CT_StockItems", "PKStockItem");
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Lot).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ExpiresOn).HasColumnType("date");
        builder.Property(x => x.MinimumStock).HasPrecision(12, 3);
        builder.Property(x => x.CurrentStock).HasPrecision(12, 3);
        builder.ToTable(t => t.HasCheckConstraint("CK_StockItems_Stock", "[MinimumStock] >= 0 AND [CurrentStock] >= 0"));
    }
}
public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        ClinicalRecordMapping.Base(builder, "IX_StockMovements", "PKStockMovement");
        builder.Property(x => x.StockItemId).HasColumnName("FKStockItem");
        builder.HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.Balance).HasPrecision(12, 3);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => new { x.StockItemId, x.Id });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Quantity", "[Quantity] > 0 AND [Balance] >= 0");
            t.HasCheckConstraint("CK_StockMovements_Kind", "[Kind] BETWEEN 0 AND 2");
        });
    }
}
