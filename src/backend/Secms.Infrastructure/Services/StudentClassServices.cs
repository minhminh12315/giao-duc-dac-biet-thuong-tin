using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class StudentService
{
    private readonly SecmsDbContext _db;
    private readonly IFileStorage _files;
    private readonly ICurrentUser _current;

    public StudentService(SecmsDbContext db, IFileStorage files, ICurrentUser current)
    {
        _db = db;
        _files = files;
        _current = current;
    }

    public async Task<List<StudentDto>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var q = _db.Students.AsNoTracking().Include(s => s.Facility).AsQueryable();
        if (_current.IsTeacher && !_current.IsAdmin)
        {
            if (_current.TeacherProfileId is not Guid tid)
                return new List<StudentDto>();
            var studentIds = await _db.SessionStudents
                .Where(ss => ss.Session.TeacherProfileId == tid)
                .Select(ss => ss.StudentId)
                .Distinct()
                .ToListAsync(ct);
            q = q.Where(s => studentIds.Contains(s.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => s.FullName.Contains(search));

        return await q.OrderBy(s => s.FullName)
            .Select(s => new StudentDto(s.Id, s.FullName, s.DateOfBirth, s.Gender, s.FacilityId, s.Facility.Name, s.Facility.Type, s.GuardianName, s.GuardianPhone, s.MedicalNotes, s.Status))
            .ToListAsync(ct);
    }

    public async Task EnsureTeacherCanAccessStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        if (_current.IsAdmin) return;
        if (!_current.IsTeacher || _current.TeacherProfileId is not Guid tid)
            throw new UnauthorizedAccessException("Không có quyền truy cập học sinh này.");
        var ok = await _db.SessionStudents.AnyAsync(
            ss => ss.StudentId == studentId && ss.Session.TeacherProfileId == tid, ct);
        if (!ok) throw new UnauthorizedAccessException("Không có quyền truy cập học sinh này.");
    }

    public async Task<StudentDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureTeacherCanAccessStudentAsync(id, ct);
        var s = await _db.Students.AsNoTracking().Include(x => x.Facility).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;
        return new StudentDto(s.Id, s.FullName, s.DateOfBirth, s.Gender, s.FacilityId, s.Facility.Name, s.Facility.Type, s.GuardianName, s.GuardianPhone, s.MedicalNotes, s.Status);
    }

    public async Task<StudentDto> CreateAsync(UpsertStudentRequest req, CancellationToken ct = default)
    {
        if (!await _db.Facilities.AnyAsync(f => f.Id == req.FacilityId, ct))
            throw new InvalidOperationException("Cơ sở không tồn tại.");
        var s = new Student
        {
            FullName = req.FullName,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            FacilityId = req.FacilityId,
            GuardianName = req.GuardianName,
            GuardianPhone = req.GuardianPhone,
            MedicalNotes = req.MedicalNotes,
            Status = req.Status
        };
        _db.Students.Add(s);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(s.Id, ct))!;
    }

    public async Task<StudentDto?> UpdateAsync(Guid id, UpsertStudentRequest req, CancellationToken ct = default)
    {
        var s = await _db.Students.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;
        s.FullName = req.FullName;
        s.DateOfBirth = req.DateOfBirth;
        s.Gender = req.Gender;
        s.FacilityId = req.FacilityId;
        s.GuardianName = req.GuardianName;
        s.GuardianPhone = req.GuardianPhone;
        s.MedicalNotes = req.MedicalNotes;
        s.Status = req.Status;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Students.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return false;
        s.IsDeleted = true;
        s.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<DocumentDto>> ListDocumentsAsync(Guid studentId, CancellationToken ct = default) =>
        await _db.StudentDocuments.AsNoTracking().Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(d.Id, d.DocumentType.ToString(), d.FileName, d.ContentType, d.SizeBytes, d.UploadedAt, d.StoragePath))
            .ToListAsync(ct);

    public async Task<DocumentDto> UploadDocumentAsync(Guid studentId, StudentDocumentType type, Stream stream, string fileName, string contentType, string userId, CancellationToken ct = default)
    {
        _ = await _db.Students.FirstOrDefaultAsync(x => x.Id == studentId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy học sinh.");
        var stored = await _files.SaveAsync(stream, fileName, contentType, $"students/{studentId}", ct);
        var doc = new StudentDocument
        {
            StudentId = studentId,
            DocumentType = type,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            StoragePath = stored.StoragePath,
            SizeBytes = stored.SizeBytes,
            UploadedByUserId = userId
        };
        _db.StudentDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        return new DocumentDto(doc.Id, doc.DocumentType.ToString(), doc.FileName, doc.ContentType, doc.SizeBytes, doc.UploadedAt, doc.StoragePath);
    }

    public async Task<ProgressDto?> GetProgressAsync(Guid studentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        await EnsureTeacherCanAccessStudentAsync(studentId, ct);
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null) return null;

        var q = _db.AttendanceRecords.AsNoTracking()
            .Include(a => a.Comment)
            .Include(a => a.Session).ThenInclude(s => s.TeacherProfile)
            .Where(a => a.StudentId == studentId && a.Status != AttendanceStatus.NotMarked);

        if (from.HasValue) q = q.Where(a => a.Session.StartAt >= from);
        if (to.HasValue) q = q.Where(a => a.Session.StartAt < to);

        var records = await q.OrderBy(a => a.Session.StartAt).ToListAsync(ct);
        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
        var total = present + absent + excused;
        var comments = records.Where(r => r.Comment != null).ToList();
        var moods = comments.Where(c => c.Comment!.MoodOrLevel.HasValue).ToList();

        return new ProgressDto(
            student.Id,
            student.FullName,
            present,
            absent,
            excused,
            total == 0 ? 0 : Math.Round(100.0 * present / total, 1),
            comments.Count,
            moods.Count == 0 ? null : Math.Round(moods.Average(m => m.Comment!.MoodOrLevel!.Value), 2),
            moods.Select(m => new MoodPointDto(m.Session.StartAt, m.Comment!.MoodOrLevel!.Value)).ToList(),
            comments.Select(c => new CommentFeedItemDto(
                c.Session.StartAt,
                c.Session.TeacherProfile.FullName,
                c.Comment!.Content,
                c.Comment.MoodOrLevel,
                c.Status)).OrderByDescending(x => x.At).ToList());
    }
}

public class ClassGroupService
{
    private readonly SecmsDbContext _db;
    public ClassGroupService(SecmsDbContext db) => _db = db;

    public async Task<List<ClassGroupDto>> ListAsync(CancellationToken ct = default)
    {
        var groups = await _db.ClassGroups.AsNoTracking()
            .Include(c => c.Facility)
            .Include(c => c.ClassGroupStudents)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return groups.Select(Map).ToList();
    }

    public async Task<ClassGroupDto> CreateAsync(UpsertClassGroupRequest req, CancellationToken ct = default)
    {
        var e = new ClassGroup
        {
            Name = req.Name,
            FacilityId = req.FacilityId,
            ServiceType = req.ServiceType,
            IsActive = req.IsActive
        };
        _db.ClassGroups.Add(e);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(e.Id, ct))!;
    }

    public async Task<ClassGroupDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClassGroups.AsNoTracking()
            .Include(c => c.Facility)
            .Include(c => c.ClassGroupStudents)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return e == null ? null : Map(e);
    }

    public async Task<ClassGroupDto?> UpdateAsync(Guid id, UpsertClassGroupRequest req, CancellationToken ct = default)
    {
        var e = await _db.ClassGroups.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e == null) return null;
        e.Name = req.Name;
        e.FacilityId = req.FacilityId;
        e.ServiceType = req.ServiceType;
        e.IsActive = req.IsActive;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ClassGroupDto?> SetStudentsAsync(Guid id, List<Guid> studentIds, CancellationToken ct = default)
    {
        var e = await _db.ClassGroups.Include(c => c.ClassGroupStudents).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e == null) return null;
        _db.ClassGroupStudents.RemoveRange(e.ClassGroupStudents);
        foreach (var sid in studentIds.Distinct())
        {
            _db.ClassGroupStudents.Add(new ClassGroupStudent { ClassGroupId = id, StudentId = sid });
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClassGroups.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (e == null) return false;
        e.IsDeleted = true;
        e.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ClassGroupDto Map(ClassGroup c) => new(
        c.Id, c.Name, c.FacilityId, c.Facility?.Name, c.ServiceType, c.IsActive,
        c.ClassGroupStudents.Where(x => x.LeftAt == null).Select(x => x.StudentId).ToList());
}
