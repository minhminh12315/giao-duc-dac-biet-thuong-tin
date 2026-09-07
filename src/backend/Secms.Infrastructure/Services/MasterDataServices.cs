using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Infrastructure.Identity;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class FacilityService
{
    private readonly SecmsDbContext _db;
    public FacilityService(SecmsDbContext db) => _db = db;

    public async Task<List<FacilityDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Facilities.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new FacilityDto(x.Id, x.Name, x.Type, x.Address, x.ContactPhone, x.TravelBufferMinutesOverride, x.IsActive))
            .ToListAsync(ct);

    public async Task<FacilityDto> CreateAsync(UpsertFacilityRequest req, CancellationToken ct = default)
    {
        var e = new Facility
        {
            Name = req.Name,
            Type = req.Type,
            Address = req.Address,
            ContactPhone = req.ContactPhone,
            TravelBufferMinutesOverride = req.TravelBufferMinutesOverride,
            IsActive = req.IsActive
        };
        _db.Facilities.Add(e);
        await _db.SaveChangesAsync(ct);
        return new FacilityDto(e.Id, e.Name, e.Type, e.Address, e.ContactPhone, e.TravelBufferMinutesOverride, e.IsActive);
    }

    public async Task<FacilityDto?> UpdateAsync(Guid id, UpsertFacilityRequest req, CancellationToken ct = default)
    {
        var e = await _db.Facilities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return null;
        e.Name = req.Name;
        e.Type = req.Type;
        e.Address = req.Address;
        e.ContactPhone = req.ContactPhone;
        e.TravelBufferMinutesOverride = req.TravelBufferMinutesOverride;
        e.IsActive = req.IsActive;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new FacilityDto(e.Id, e.Name, e.Type, e.Address, e.ContactPhone, e.TravelBufferMinutesOverride, e.IsActive);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Facilities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return false;
        e.IsDeleted = true;
        e.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public class TeacherService
{
    private readonly SecmsDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IFileStorage _files;

    public TeacherService(SecmsDbContext db, UserManager<AppUser> users, IFileStorage files)
    {
        _db = db;
        _users = users;
        _files = files;
    }

    public async Task<List<TeacherDto>> ListAsync(CancellationToken ct = default)
    {
        var teachers = await _db.TeacherProfiles.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct);
        var result = new List<TeacherDto>();
        foreach (var t in teachers)
        {
            var user = await _users.FindByIdAsync(t.UserId);
            result.Add(new TeacherDto(t.Id, t.UserId, user?.UserName ?? "", user?.Email ?? "", t.FullName, t.Phone, t.Specialization, t.HireDate, user?.IsActive ?? true));
        }
        return result;
    }

    public async Task<TeacherDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.TeacherProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t == null) return null;
        var user = await _users.FindByIdAsync(t.UserId);
        return new TeacherDto(t.Id, t.UserId, user?.UserName ?? "", user?.Email ?? "", t.FullName, t.Phone, t.Specialization, t.HireDate, user?.IsActive ?? true);
    }

    public async Task<TeacherDto> CreateAsync(CreateTeacherRequest req, CancellationToken ct = default)
    {
        var user = new AppUser
        {
            UserName = req.UserName,
            Email = req.Email,
            DisplayName = req.FullName,
            IsActive = true,
            EmailConfirmed = true
        };
        var create = await _users.CreateAsync(user, req.Password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, AppRoles.Teacher);

        var profile = new TeacherProfile
        {
            UserId = user.Id,
            FullName = req.FullName,
            Phone = req.Phone,
            Specialization = req.Specialization,
            HireDate = req.HireDate,
            Address = req.Address,
            Notes = req.Notes
        };
        _db.TeacherProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        user.TeacherProfileId = profile.Id;
        await _users.UpdateAsync(user);

        return new TeacherDto(profile.Id, user.Id, user.UserName!, user.Email!, profile.FullName, profile.Phone, profile.Specialization, profile.HireDate, true);
    }

    public async Task<TeacherDto?> UpdateAsync(Guid id, UpdateTeacherRequest req, CancellationToken ct = default)
    {
        var t = await _db.TeacherProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t == null) return null;
        t.FullName = req.FullName;
        t.Phone = req.Phone;
        t.Specialization = req.Specialization;
        t.HireDate = req.HireDate;
        t.Address = req.Address;
        t.Notes = req.Notes;
        t.UpdatedAt = DateTimeOffset.UtcNow;

        var user = await _users.FindByIdAsync(t.UserId);
        if (user != null)
        {
            user.DisplayName = req.FullName;
            user.IsActive = req.IsActive;
            await _users.UpdateAsync(user);
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<List<DocumentDto>> ListDocumentsAsync(Guid teacherId, CancellationToken ct = default) =>
        await _db.TeacherDocuments.AsNoTracking().Where(d => d.TeacherProfileId == teacherId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(d.Id, d.DocumentType.ToString(), d.FileName, d.ContentType, d.SizeBytes, d.UploadedAt, d.StoragePath))
            .ToListAsync(ct);

    public async Task<DocumentDto> UploadDocumentAsync(Guid teacherId, TeacherDocumentType type, Stream stream, string fileName, string contentType, string userId, CancellationToken ct = default)
    {
        _ = await _db.TeacherProfiles.FirstOrDefaultAsync(x => x.Id == teacherId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy giáo viên.");
        var stored = await _files.SaveAsync(stream, fileName, contentType, $"teachers/{teacherId}", ct);
        var doc = new TeacherDocument
        {
            TeacherProfileId = teacherId,
            DocumentType = type,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            StoragePath = stored.StoragePath,
            PublicUrl = stored.PublicUrl,
            SizeBytes = stored.SizeBytes,
            UploadedByUserId = userId
        };
        _db.TeacherDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        return new DocumentDto(doc.Id, doc.DocumentType.ToString(), doc.FileName, doc.ContentType, doc.SizeBytes, doc.UploadedAt, doc.StoragePath);
    }

    public async Task<bool> DeleteDocumentAsync(Guid teacherId, Guid docId, CancellationToken ct = default)
    {
        var doc = await _db.TeacherDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.TeacherProfileId == teacherId, ct);
        if (doc == null) return false;
        await _files.DeleteAsync(doc.StoragePath, ct);
        _db.TeacherDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
