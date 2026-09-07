using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Secms.Domain.Entities;
using Secms.Infrastructure.Identity;

namespace Secms.Infrastructure.Persistence;

public class SecmsDbContext : IdentityDbContext<AppUser>
{
    public SecmsDbContext(DbContextOptions<SecmsDbContext> options) : base(options) { }

    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<TeacherDocument> TeacherDocuments => Set<TeacherDocument>();
    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<ClassGroupStudent> ClassGroupStudents => Set<ClassGroupStudent>();
    public DbSet<TeachingSession> Sessions => Set<TeachingSession>();
    public DbSet<SessionStudent> SessionStudents => Set<SessionStudent>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<TeachingComment> TeachingComments => Set<TeachingComment>();
    public DbSet<TuitionRate> TuitionRates => Set<TuitionRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TeacherProfile>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Facility>(e => e.HasQueryFilter(x => !x.IsDeleted));
        builder.Entity<Student>(e =>
        {
            e.HasOne(x => x.Facility).WithMany(x => x.Students).HasForeignKey(x => x.FacilityId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<ClassGroup>(e => e.HasQueryFilter(x => !x.IsDeleted));

        builder.Entity<ClassGroupStudent>(e =>
        {
            e.HasKey(x => new { x.ClassGroupId, x.StudentId });
            e.HasOne(x => x.ClassGroup).WithMany(x => x.ClassGroupStudents).HasForeignKey(x => x.ClassGroupId);
            e.HasOne(x => x.Student).WithMany(x => x.ClassGroupStudents).HasForeignKey(x => x.StudentId);
        });

        builder.Entity<TeachingSession>(e =>
        {
            e.ToTable("sessions");
            e.HasOne(x => x.TeacherProfile).WithMany(x => x.Sessions).HasForeignKey(x => x.TeacherProfileId);
            e.HasOne(x => x.Facility).WithMany(x => x.Sessions).HasForeignKey(x => x.FacilityId);
            e.HasOne(x => x.ClassGroup).WithMany().HasForeignKey(x => x.ClassGroupId);
            e.HasIndex(x => new { x.TeacherProfileId, x.StartAt, x.EndAt });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<SessionStudent>(e =>
        {
            e.HasKey(x => new { x.SessionId, x.StudentId });
            e.HasOne(x => x.Session).WithMany(x => x.SessionStudents).HasForeignKey(x => x.SessionId);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
        });

        builder.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Session).WithMany(x => x.AttendanceRecords).HasForeignKey(x => x.SessionId);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId);
            e.HasOne(x => x.Comment).WithOne(x => x.AttendanceRecord).HasForeignKey<TeachingComment>(x => x.AttendanceRecordId);
        });

        builder.Entity<TuitionRate>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.StudentId, x.PeriodYear, x.PeriodMonth, x.Version }).IsUnique();
        });

        builder.Entity<InvoiceLine>(e =>
        {
            e.Property(x => x.UnitRate).HasPrecision(18, 2);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
