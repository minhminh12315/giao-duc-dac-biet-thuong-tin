using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secms.Application.Dtos;
using Secms.Infrastructure.Services;

namespace Secms.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly SchedulingService _svc;
    public SessionsController(SchedulingService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<SessionDto>>> List(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? teacherId,
        [FromQuery] Guid? facilityId,
        CancellationToken ct) =>
        Ok(await _svc.ListAsync(from, to, teacherId, facilityId, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] UpsertSessionRequest req, CancellationToken ct)
    {
        var (session, conflicts, saved) = await _svc.CreateAsync(req, ct);
        if (!saved) return Conflict(new { message = "Xung đột lịch.", conflicts });
        return Ok(new { session, conflicts });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertSessionRequest req, CancellationToken ct)
    {
        var (session, conflicts, saved) = await _svc.UpdateAsync(id, req, ct);
        if (session == null && conflicts.Count == 0) return NotFound();
        if (!saved) return Conflict(new { message = "Xung đột lịch.", conflicts });
        return Ok(new { session, conflicts });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        await _svc.CancelAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("check-conflicts")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ScheduleConflictDto>>> Check([FromBody] UpsertSessionRequest req, [FromQuery] Guid? excludeId, CancellationToken ct) =>
        Ok(await _svc.CheckConflictsAsync(req, excludeId, ct));

    [HttpGet("{id:guid}/attendance")]
    public async Task<ActionResult<List<AttendanceItemDto>>> GetAttendance(Guid id, [FromServices] AttendanceService attendance, CancellationToken ct) =>
        Ok(await attendance.GetAsync(id, ct));

    [HttpPut("{id:guid}/attendance/draft")]
    public async Task<IActionResult> Draft(Guid id, [FromBody] SaveAttendanceRequest req, [FromServices] AttendanceService attendance, CancellationToken ct)
    {
        await attendance.SaveDraftAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/attendance/finalize")]
    public async Task<IActionResult> Finalize(Guid id, [FromBody] SaveAttendanceRequest req, [FromServices] AttendanceService attendance, CancellationToken ct)
    {
        await attendance.FinalizeAsync(id, req, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/schedules")]
[Authorize(Roles = "Admin")]
public class SchedulesController : ControllerBase
{
    private readonly SchedulingService _svc;
    public SchedulesController(SchedulingService svc) => _svc = svc;

    [HttpPost("clone-week")]
    public async Task<ActionResult<CloneWeekResult>> Clone([FromBody] CloneWeekRequest req, CancellationToken ct) =>
        Ok(await _svc.CloneWeekAsync(req, ct));
}

[ApiController]
[Route("api/tuition-rates")]
[Authorize(Roles = "Admin")]
public class TuitionRatesController : ControllerBase
{
    private readonly BillingService _svc;
    public TuitionRatesController(BillingService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<TuitionRateDto>>> List(CancellationToken ct) => Ok(await _svc.ListRatesAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TuitionRateDto>> Create([FromBody] UpsertTuitionRateRequest req, CancellationToken ct) =>
        Ok(await _svc.CreateRateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TuitionRateDto>> Update(Guid id, [FromBody] UpsertTuitionRateRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateRateAsync(id, req, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _svc.DeleteRateAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("resolve")]
    public async Task<ActionResult<object>> Resolve([FromQuery] Guid studentId, [FromQuery] Guid sessionId, CancellationToken ct) =>
        Ok(new { amount = await _svc.ResolvePreviewAsync(studentId, sessionId, ct) });
}

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = "Admin")]
public class InvoicesController : ControllerBase
{
    private readonly BillingService _svc;
    public InvoicesController(BillingService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<InvoiceDto>>> List([FromQuery] Guid? studentId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        Ok(await _svc.ListInvoicesAsync(studentId, year, month, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id, CancellationToken ct)
    {
        var i = await _svc.GetInvoiceAsync(id, ct);
        return i == null ? NotFound() : Ok(i);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<InvoiceDto>> Generate([FromBody] GenerateInvoiceRequest req, CancellationToken ct) =>
        Ok(await _svc.GenerateAsync(req, ct));

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct) =>
        await _svc.VoidAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly Application.Interfaces.IFileStorage _files;
    public FilesController(Application.Interfaces.IFileStorage files) => _files = files;

    [HttpGet("{*path}")]
    public async Task<IActionResult> Get(string path, CancellationToken ct)
    {
        var stream = await _files.OpenReadAsync(path, ct);
        var contentType = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream";
        return File(stream, contentType);
    }
}
