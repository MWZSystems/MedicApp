using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Data;

public class MedicDbContext(DbContextOptions<MedicDbContext> options) : DbContext(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<AvailabilityWindow> Windows => Set<AvailabilityWindow>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalLeave> Leaves => Set<MedicalLeave>();
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<LeaveDocument> LeaveDocuments => Set<LeaveDocument>();
    public DbSet<LaborEvent> LaborEvents => Set<LaborEvent>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedPerson>().ToTable("SY_People", "dbo", t => t.ExcludeFromMigrations()).HasKey(x => x.Id);
        modelBuilder.Entity<SharedUser>().ToTable("SC_Users", "dbo", t => t.ExcludeFromMigrations()).HasKey(x => x.Id);
        modelBuilder.Entity<SharedUser>().Property(x => x.Id).HasMaxLength(450);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedicDbContext).Assembly);
    }
}
