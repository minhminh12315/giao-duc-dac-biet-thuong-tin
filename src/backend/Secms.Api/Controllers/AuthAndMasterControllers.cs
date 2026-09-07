using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Infrastructure.Services;

namespace Secms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ICurrentUser _current;

    public AuthController(AuthService auth, ICurrentUser current)
    {
        _auth = auth;
        _current = current;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (result == null) return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu." });
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        if (_current.UserId == null) return Unauthorized();
        var me = await _auth.MeAsync(_current.UserId, ct);
        return me == null ? Unauthorized() : Ok(me);
    }
}

[ApiController]
[Route("api/facilities")]
[Authorize]
public class FacilitiesController : ControllerBase
{
    private readonly FacilityService _svc;
    public FacilitiesController(FacilityService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<FacilityDto>>> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FacilityDto>> Create([FromBody] UpsertFacilityRequest req, CancellationToken ct) =>
        Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FacilityDto>> Update(Guid id, [FromBody] UpsertFacilityRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/teachers")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly TeacherService _svc;
    private readonly ICurrentUser _current;
    public TeachersController(TeacherService svc, ICurrentUser current)
    {
        _svc = svc;
        _current = current;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<TeacherDto>>> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpGet("me")]
    public async Task<ActionResult<TeacherDto>> Me(CancellationToken ct)
    {
        if (_current.TeacherProfileId == null) return NotFound();
        var t = await _svc.GetAsync(_current.TeacherProfileId.Value, ct);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeacherDto>> Get(Guid id, CancellationToken ct)
    {
        var t = await _svc.GetAsync(id, ct);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeacherDto>> Create([FromBody] CreateTeacherRequest req, CancellationToken ct) =>
        Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeacherDto>> Update(Guid id, [FromBody] UpdateTeacherRequest req, CancellationToken ct)
    {
        var t = await _svc.UpdateAsync(id, req, ct);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> Docs(Guid id, CancellationToken ct)
    {
        if (!_current.IsAdmin && _current.TeacherProfileId != id) return Forbid();
        return Ok(await _svc.ListDocumentsAsync(id, ct));
    }

    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DocumentDto>> Upload(Guid id, IFormFile file, [FromForm] string documentType, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("File trống.");
        if (!Enum.TryParse<Domain.Enums.TeacherDocumentType>(documentType, true, out var type))
            type = Domain.Enums.TeacherDocumentType.Other;
        await using var stream = file.OpenReadStream();
        var doc = await _svc.UploadDocumentAsync(id, type, stream, file.FileName, file.ContentType, _current.UserId!, ct);
        return Ok(doc);
    }

    [HttpDelete("{id:guid}/documents/{docId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDoc(Guid id, Guid docId, CancellationToken ct) =>
        await _svc.DeleteDocumentAsync(id, docId, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly StudentService _svc;
    private readonly ICurrentUser _current;
    private readonly IFileStorage _files;
    public StudentsController(StudentService svc, ICurrentUser current, IFileStorage files)
    {
        _svc = svc;
        _current = current;
        _files = files;
    }

    [HttpGet]
    public async Task<ActionResult<List<StudentDto>>> List([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _svc.ListAsync(search, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentDto>> Get(Guid id, CancellationToken ct)
    {
        var s = await _svc.GetAsync(id, ct);
        return s == null ? NotFound() : Ok(s);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentDto>> Create([FromBody] UpsertStudentRequest req, CancellationToken ct) =>
        Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentDto>> Update(Guid id, [FromBody] UpsertStudentRequest req, CancellationToken ct)
    {
        var s = await _svc.UpdateAsync(id, req, ct);
        return s == null ? NotFound() : Ok(s);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/documents")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<DocumentDto>>> Docs(Guid id, CancellationToken ct) =>
        Ok(await _svc.ListDocumentsAsync(id, ct));

    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DocumentDto>> Upload(Guid id, IFormFile file, [FromForm] string documentType, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequest("File trống.");
        if (!Enum.TryParse<Domain.Enums.StudentDocumentType>(documentType, true, out var type))
            type = Domain.Enums.StudentDocumentType.Other;
        await using var stream = file.OpenReadStream();
        return Ok(await _svc.UploadDocumentAsync(id, type, stream, file.FileName, file.ContentType, _current.UserId!, ct));
    }

    [HttpGet("{id:guid}/progress")]
    public async Task<ActionResult<ProgressDto>> Progress(Guid id, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var p = await _svc.GetProgressAsync(id, from, to, ct);
        return p == null ? NotFound() : Ok(p);
    }
}

[ApiController]
[Route("api/class-groups")]
[Authorize(Roles = "Admin")]
public class ClassGroupsController : ControllerBase
{
    private readonly ClassGroupService _svc;
    public ClassGroupsController(ClassGroupService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<ClassGroupDto>>> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ClassGroupDto>> Create([FromBody] UpsertClassGroupRequest req, CancellationToken ct) =>
        Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClassGroupDto>> Update(Guid id, [FromBody] UpsertClassGroupRequest req, CancellationToken ct)
    {
        var r = await _svc.UpdateAsync(id, req, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpPut("{id:guid}/students")]
    public async Task<ActionResult<ClassGroupDto>> SetStudents(Guid id, [FromBody] SetClassGroupStudentsRequest req, CancellationToken ct)
    {
        var r = await _svc.SetStudentsAsync(id, req.StudentIds, ct);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
