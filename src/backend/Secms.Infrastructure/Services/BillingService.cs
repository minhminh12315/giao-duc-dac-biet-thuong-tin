using Microsoft.EntityFrameworkCore;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Domain.Services;
using Secms.Infrastructure;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class BillingService
{
    private readonly SecmsDbContext _db;
    private readonly ICurrentUser _current;

    public BillingService(SecmsDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<TuitionRateDto>> ListRatesAsync(CancellationToken ct = default) =>
        await _db.TuitionRates.AsNoTracking().OrderByDescending(r => r.EffectiveFrom)
            .Select(r => new TuitionRateDto(r.Id, r.Scope, r.StudentId, r.ClassGroupId, r.ServiceType, r.Amount, r.Currency, r.EffectiveFrom, r.EffectiveTo, r.IsActive))
            .ToListAsync(ct);

    public async Task<TuitionRateDto> CreateRateAsync(UpsertTuitionRateRequest req, CancellationToken ct = default)
    {
        ValidateRate(req);
        var e = new TuitionRate
        {
            Scope = req.Scope,
            StudentId = req.StudentId,
            ClassGroupId = req.ClassGroupId,
            ServiceType = req.ServiceType,
            Amount = req.Amount,
            EffectiveFrom = req.EffectiveFrom,
            EffectiveTo = req.EffectiveTo,
            IsActive = req.IsActive
        };
        _db.TuitionRates.Add(e);
        await _db.SaveChangesAsync(ct);
        return new TuitionRateDto(e.Id, e.Scope, e.StudentId, e.ClassGroupId, e.ServiceType, e.Amount, e.Currency, e.EffectiveFrom, e.EffectiveTo, e.IsActive);
    }

    public async Task<TuitionRateDto?> UpdateRateAsync(Guid id, UpsertTuitionRateRequest req, CancellationToken ct = default)
    {
        ValidateRate(req);
        var e = await _db.TuitionRates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return null;
        e.Scope = req.Scope;
        e.StudentId = req.StudentId;
        e.ClassGroupId = req.ClassGroupId;
        e.ServiceType = req.ServiceType;
        e.Amount = req.Amount;
        e.EffectiveFrom = req.EffectiveFrom;
        e.EffectiveTo = req.EffectiveTo;
        e.IsActive = req.IsActive;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new TuitionRateDto(e.Id, e.Scope, e.StudentId, e.ClassGroupId, e.ServiceType, e.Amount, e.Currency, e.EffectiveFrom, e.EffectiveTo, e.IsActive);
    }

    public async Task<bool> DeleteRateAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.TuitionRates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return false;
        e.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<decimal> ResolvePreviewAsync(Guid studentId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .IgnoreQueryFilters()
            .Include(s => s.ClassGroup)
            .Include(s => s.Facility)
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Session not found");
        var rates = await _db.TuitionRates.AsNoTracking().ToListAsync(ct);
        var serviceType = RateResolver.DeriveServiceType(session);
        var date = TimeZones.ToVietnamDate(session.StartAt);
        var rate = RateResolver.Resolve(rates, studentId, session.ClassGroupId, serviceType, date)
            ?? throw new InvalidOperationException("Chưa cấu hình đơn giá phù hợp.");
        return rate.Amount;
    }

    public async Task<InvoiceDto> GenerateAsync(GenerateInvoiceRequest req, CancellationToken ct = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == req.StudentId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy học sinh.");

        var tz = TimeZones.Vietnam;
        var localStart = new DateTime(req.Year, req.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddMonths(1);
        var periodStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, tz), TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, tz), TimeSpan.Zero);

        var presents = await _db.AttendanceRecords
            .Include(a => a.Session).ThenInclude(s => s.ClassGroup)
            .Include(a => a.Session).ThenInclude(s => s.Facility)
            .Where(a => a.StudentId == req.StudentId
                        && a.Status == AttendanceStatus.Present
                        && a.Session.Status == SessionStatus.Completed
                        && a.Session.AttendanceFinalizedAt != null
                        && a.Session.StartAt >= periodStart
                        && a.Session.StartAt < periodEnd)
            .ToListAsync(ct);

        var rates = await _db.TuitionRates.AsNoTracking().ToListAsync(ct);
        var lines = new List<InvoiceLine>();
        foreach (var att in presents)
        {
            var serviceType = RateResolver.DeriveServiceType(att.Session);
            var date = TimeZones.ToVietnamDate(att.Session.StartAt);
            var rate = RateResolver.Resolve(rates, req.StudentId, att.Session.ClassGroupId, serviceType, date)
                ?? throw new InvalidOperationException($"Chưa có đơn giá cho buổi {att.Session.StartAt:dd/MM/yyyy HH:mm}.");
            lines.Add(new InvoiceLine
            {
                SessionId = att.SessionId,
                AttendanceRecordId = att.Id,
                UnitRate = rate.Amount,
                Amount = rate.Amount,
                Description = $"{att.Session.Title ?? "Ca học"} · {TimeZoneInfo.ConvertTime(att.Session.StartAt, tz):dd/MM HH:mm}"
            });
        }

        var existing = await _db.Invoices
            .Where(i => i.StudentId == req.StudentId && i.PeriodYear == req.Year && i.PeriodMonth == req.Month)
            .ToListAsync(ct);

        if (existing.Any(i => i.Status == InvoiceStatus.Issued) && !req.Regenerate)
            throw new InvalidOperationException("Đã có hóa đơn tháng này. Dùng regenerate=true để tạo lại.");

        foreach (var old in existing.Where(i => i.Status == InvoiceStatus.Issued))
            old.Status = InvoiceStatus.Void;

        var version = existing.Count == 0 ? 1 : existing.Max(i => i.Version) + 1;
        var invoice = new Invoice
        {
            StudentId = req.StudentId,
            PeriodYear = req.Year,
            PeriodMonth = req.Month,
            TotalAmount = lines.Sum(l => l.Amount),
            Status = InvoiceStatus.Issued,
            Version = version,
            GeneratedByUserId = _current.UserId,
            Lines = lines
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return (await GetInvoiceAsync(invoice.Id, ct))!;
    }

    public async Task<List<InvoiceDto>> ListInvoicesAsync(Guid? studentId, int? year, int? month, CancellationToken ct = default)
    {
        var q = _db.Invoices.AsNoTracking().Include(i => i.Student).Include(i => i.Lines).ThenInclude(l => l.Session).AsQueryable();
        if (studentId.HasValue) q = q.Where(i => i.StudentId == studentId);
        if (year.HasValue) q = q.Where(i => i.PeriodYear == year);
        if (month.HasValue) q = q.Where(i => i.PeriodMonth == month);
        var list = await q.OrderByDescending(i => i.GeneratedAt).ToListAsync(ct);
        return list.Select(MapInvoice).ToList();
    }

    public async Task<InvoiceDto?> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var i = await _db.Invoices.AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Lines).ThenInclude(l => l.Session)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return i == null ? null : MapInvoice(i);
    }

    public async Task<bool> VoidAsync(Guid id, CancellationToken ct = default)
    {
        var i = await _db.Invoices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i == null) return false;
        i.Status = InvoiceStatus.Void;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateRate(UpsertTuitionRateRequest req)
    {
        if (req.Amount < 0) throw new InvalidOperationException("Đơn giá không hợp lệ.");
        switch (req.Scope)
        {
            case TuitionRateScope.Student when req.StudentId == null:
            case TuitionRateScope.ClassGroup when req.ClassGroupId == null:
            case TuitionRateScope.ServiceType when req.ServiceType == null:
                throw new InvalidOperationException("Thiếu tham chiếu theo scope đơn giá.");
        }
    }

    private static InvoiceDto MapInvoice(Invoice i) => new(
        i.Id, i.StudentId, i.Student.FullName, i.PeriodYear, i.PeriodMonth, i.TotalAmount, i.Currency, i.Status, i.Version, i.GeneratedAt,
        i.Lines.Select(l => new InvoiceLineDto(l.Id, l.SessionId, l.Session.StartAt, l.UnitRate, l.Amount, l.Description)).ToList());
}
