using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Infrastructure.Identity;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Seed;

public static class DbSeeder
{
    private const string DemoTag = "#demo-week";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecmsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        foreach (var role in new[] { AppRoles.Admin, AppRoles.Teacher })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        if (await users.FindByNameAsync("admin") == null)
        {
            var admin = new AppUser
            {
                UserName = "admin",
                Email = "admin@secms.local",
                DisplayName = "Quản trị viên",
                EmailConfirmed = true,
                IsActive = true
            };
            await users.CreateAsync(admin, "Admin@123");
            await users.AddToRoleAsync(admin, AppRoles.Admin);
        }

        await EnsureCoreCatalogAsync(db, users);
        await SeedCurrentWeekDemoAsync(db);
    }

    private static async Task EnsureCoreCatalogAsync(SecmsDbContext db, UserManager<AppUser> users)
    {
        if (!await db.Facilities.IgnoreQueryFilters().AnyAsync())
        {
            db.Facilities.AddRange(
                new Facility
                {
                    Name = "Cơ sở chính Thường Tín",
                    Type = FacilityType.MainCenter,
                    Address = "Thường Tín, Hà Nội",
                    ContactPhone = "0240000001"
                },
                new Facility
                {
                    Name = "Trường đối tác C",
                    Type = FacilityType.PartnerSchool,
                    Address = "Huyện Thường Tín",
                    ContactPhone = "0240000002"
                },
                new Facility
                {
                    Name = "Trường đối tác D",
                    Type = FacilityType.PartnerSchool,
                    Address = "Xã Ninh Sở",
                    ContactPhone = "0240000003"
                });
            await db.SaveChangesAsync();
        }

        var facilities = await db.Facilities.OrderBy(f => f.Name).ToListAsync();
        var main = facilities.First(f => f.Type == FacilityType.MainCenter);
        var partners = facilities.Where(f => f.Type == FacilityType.PartnerSchool).ToList();

        await EnsureTeacherAsync(db, users, "gv01", "Nguyễn Thị Giáo", "0901000001", "Giáo dục đặc biệt");
        await EnsureTeacherAsync(db, users, "gv02", "Phạm Văn Minh", "0901000002", "Ngôn ngữ trị liệu");
        await EnsureTeacherAsync(db, users, "gv03", "Lê Thị Hoa", "0901000003", "Vận động tinh");

        if (!await db.Students.AnyAsync())
        {
            var students = new[]
            {
                new Student { FullName = "Trần Văn A", FacilityId = main.Id, GuardianName = "Trần phụ huynh", GuardianPhone = "0902000001", DateOfBirth = new DateOnly(2016, 5, 1), Gender = "Nam" },
                new Student { FullName = "Lê Thị B", FacilityId = partners[0].Id, GuardianName = "Lê phụ huynh", GuardianPhone = "0902000002", DateOfBirth = new DateOnly(2017, 3, 12), Gender = "Nữ" },
                new Student { FullName = "Nguyễn Minh C", FacilityId = main.Id, GuardianName = "Nguyễn phụ huynh", GuardianPhone = "0902000003", DateOfBirth = new DateOnly(2015, 11, 20), Gender = "Nam" },
                new Student { FullName = "Hoàng Thị D", FacilityId = partners.ElementAtOrDefault(1)?.Id ?? partners[0].Id, GuardianName = "Hoàng phụ huynh", GuardianPhone = "0902000004", DateOfBirth = new DateOnly(2018, 1, 8), Gender = "Nữ" },
                new Student { FullName = "Đỗ Văn E", FacilityId = main.Id, GuardianName = "Đỗ phụ huynh", GuardianPhone = "0902000005", DateOfBirth = new DateOnly(2016, 9, 15), Gender = "Nam" },
            };
            db.Students.AddRange(students);
            await db.SaveChangesAsync();
        }

        if (!await db.ClassGroups.AnyAsync())
        {
            var students = await db.Students.OrderBy(s => s.FullName).ToListAsync();
            var classA = new ClassGroup { Name = "Lớp A — Trung tâm", FacilityId = main.Id, ServiceType = ServiceType.AtCenter };
            var classB = new ClassGroup { Name = "Lớp B — On-site C", FacilityId = partners[0].Id, ServiceType = ServiceType.AtPartnerSchool };
            var classC = new ClassGroup
            {
                Name = "Lớp C — On-site D",
                FacilityId = partners.ElementAtOrDefault(1)?.Id ?? partners[0].Id,
                ServiceType = ServiceType.AtPartnerSchool
            };
            db.ClassGroups.AddRange(classA, classB, classC);
            await db.SaveChangesAsync();

            db.ClassGroupStudents.AddRange(
                new ClassGroupStudent { ClassGroupId = classA.Id, StudentId = students[0].Id },
                new ClassGroupStudent { ClassGroupId = classA.Id, StudentId = students.ElementAtOrDefault(2)?.Id ?? students[0].Id },
                new ClassGroupStudent { ClassGroupId = classB.Id, StudentId = students.ElementAtOrDefault(1)?.Id ?? students[0].Id },
                new ClassGroupStudent { ClassGroupId = classC.Id, StudentId = students.ElementAtOrDefault(3)?.Id ?? students[0].Id });
            await db.SaveChangesAsync();
        }

        if (!await db.TuitionRates.AnyAsync())
        {
            var s1 = await db.Students.OrderBy(s => s.FullName).FirstAsync();
            db.TuitionRates.AddRange(
                new TuitionRate { Scope = TuitionRateScope.SystemDefault, Amount = 150_000, EffectiveFrom = new DateOnly(2020, 1, 1) },
                new TuitionRate { Scope = TuitionRateScope.Student, StudentId = s1.Id, Amount = 200_000, EffectiveFrom = new DateOnly(2020, 1, 1) },
                new TuitionRate { Scope = TuitionRateScope.ServiceType, ServiceType = ServiceType.AtPartnerSchool, Amount = 250_000, EffectiveFrom = new DateOnly(2020, 1, 1) });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureTeacherAsync(
        SecmsDbContext db,
        UserManager<AppUser> users,
        string userName,
        string fullName,
        string phone,
        string specialization)
    {
        var existing = await users.FindByNameAsync(userName);
        if (existing != null)
        {
            if (existing.TeacherProfileId == null)
            {
                var profile = new TeacherProfile
                {
                    UserId = existing.Id,
                    FullName = fullName,
                    Phone = phone,
                    Specialization = specialization,
                    HireDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-2))
                };
                db.TeacherProfiles.Add(profile);
                await db.SaveChangesAsync();
                existing.TeacherProfileId = profile.Id;
                await users.UpdateAsync(existing);
            }
            return;
        }

        var teacherUser = new AppUser
        {
            UserName = userName,
            Email = $"{userName}@secms.local",
            DisplayName = fullName,
            EmailConfirmed = true,
            IsActive = true
        };
        await users.CreateAsync(teacherUser, "Teacher@123");
        await users.AddToRoleAsync(teacherUser, AppRoles.Teacher);

        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            FullName = fullName,
            Phone = phone,
            Specialization = specialization,
            HireDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-1))
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        teacherUser.TeacherProfileId = teacher.Id;
        await users.UpdateAsync(teacherUser);
    }

    /// <summary>
    /// Rebuild ~20 demo sessions for the current VN week on every API start
    /// so the schedule page always has visible sample data.
    /// </summary>
    private static async Task SeedCurrentWeekDemoAsync(SecmsDbContext db)
    {
        var teachers = await db.TeacherProfiles.OrderBy(t => t.FullName).ToListAsync();
        var facilities = await db.Facilities.OrderBy(f => f.Name).ToListAsync();
        var classes = await db.ClassGroups
            .Include(c => c.ClassGroupStudents)
            .OrderBy(c => c.Name)
            .ToListAsync();
        var students = await db.Students.OrderBy(s => s.FullName).ToListAsync();

        if (teachers.Count == 0 || facilities.Count == 0 || students.Count == 0)
            return;

        var main = facilities.FirstOrDefault(f => f.Type == FacilityType.MainCenter) ?? facilities[0];
        var partners = facilities.Where(f => f.Type == FacilityType.PartnerSchool).DefaultIfEmpty(main).ToList();

        var vn = TimeSpan.FromHours(7);
        var nowVn = DateTimeOffset.UtcNow.ToOffset(vn);
        var today = nowVn.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekStart = new DateTimeOffset(monday.Year, monday.Month, monday.Day, 0, 0, 0, vn).ToUniversalTime();
        var weekEnd = weekStart.AddDays(7);

        // Refresh demo: remove prior demo tags + any non-finalized sessions in the current week
        var oldDemo = await db.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.SessionStudents)
            .Include(s => s.AttendanceRecords)
            .Where(s =>
                s.Notes == DemoTag
                || (s.StartAt >= weekStart && s.StartAt < weekEnd && s.AttendanceFinalizedAt == null && !s.AttendanceRecords.Any()))
            .ToListAsync();
        if (oldDemo.Count > 0)
        {
            db.SessionStudents.RemoveRange(oldDemo.SelectMany(s => s.SessionStudents));
            db.Sessions.RemoveRange(oldDemo);
            await db.SaveChangesAsync();
        }

        // 20 slots across Mon–Fri, mixed facilities/teachers
        var blueprint = new (int DayOffset, int Hour, int Minute, int DurationHours, int TeacherIdx, bool Partner, int ClassIdx, string Title)[]
        {
            (0, 8, 0, 2, 0, false, 0, "Can thiệp sớm — nhóm A"),
            (0, 10, 15, 2, 1, true, 1, "On-site ngôn ngữ — Trường C"),
            (0, 13, 30, 2, 2, false, 0, "Vận động tinh — cá nhân"),
            (0, 15, 45, 1, 0, false, 0, "Ôn kỹ năng xã hội"),

            (1, 8, 0, 2, 1, false, 0, "Trị liệu ngôn ngữ"),
            (1, 10, 30, 2, 0, true, 1, "On-site lớp B"),
            (1, 14, 0, 2, 2, true, 2, "On-site lớp C"),
            (1, 16, 15, 1, 1, false, 0, "Tư vấn phụ huynh"),

            (2, 8, 30, 2, 0, false, 0, "Lớp A — sáng"),
            (2, 11, 0, 1, 2, false, 0, "Đánh giá tiến trình"),
            (2, 13, 30, 2, 1, true, 1, "On-site buổi chiều"),
            (2, 15, 45, 2, 0, false, 0, "Kỹ năng sống"),

            (3, 8, 0, 2, 2, false, 0, "Vận động thô"),
            (3, 10, 15, 2, 0, true, 2, "On-site Trường D"),
            (3, 13, 0, 2, 1, false, 0, "Ngôn ngữ biểu đạt"),
            (3, 15, 30, 2, 2, true, 1, "On-site lớp B — chiều"),

            (4, 8, 0, 2, 0, false, 0, "Lớp A — cuối tuần"),
            (4, 10, 30, 2, 1, false, 0, "Nhóm giao tiếp"),
            (4, 13, 30, 2, 2, true, 1, "On-site tổng kết"),
            (4, 15, 45, 1, 0, false, 0, "Review tuần"),
        };

        var sessions = new List<TeachingSession>();
        foreach (var b in blueprint)
        {
            var teacher = teachers[b.TeacherIdx % teachers.Count];
            var facility = b.Partner ? partners[b.TeacherIdx % partners.Count] : main;
            ClassGroup? cls = classes.Count > 0 ? classes[b.ClassIdx % classes.Count] : null;
            if (cls?.FacilityId is Guid fid)
            {
                var preferred = facilities.FirstOrDefault(f => f.Id == fid);
                if (preferred != null && b.Partner == (preferred.Type == FacilityType.PartnerSchool))
                    facility = preferred;
            }

            var day = monday.AddDays(b.DayOffset);
            var start = new DateTimeOffset(day.Year, day.Month, day.Day, b.Hour, b.Minute, 0, vn).ToUniversalTime();
            var end = start.AddHours(b.DurationHours);

            var session = new TeachingSession
            {
                ClassGroupId = cls?.Id,
                TeacherProfileId = teacher.Id,
                FacilityId = facility.Id,
                StartAt = start,
                EndAt = end,
                Title = b.Title,
                Notes = DemoTag,
                Status = SessionStatus.Scheduled
            };

            var studentIds = cls?.ClassGroupStudents.Select(x => x.StudentId).ToList()
                ?? new List<Guid> { students[b.TeacherIdx % students.Count].Id };
            if (studentIds.Count == 0)
                studentIds.Add(students[0].Id);

            foreach (var sid in studentIds.Distinct().Take(3))
                session.SessionStudents.Add(new SessionStudent { StudentId = sid });

            sessions.Add(session);
        }

        db.Sessions.AddRange(sessions);
        await db.SaveChangesAsync();
    }
}
