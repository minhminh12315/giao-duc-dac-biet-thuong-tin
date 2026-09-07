using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Application.Options;
using Secms.Domain.Enums;
using Secms.Infrastructure.Identity;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly SecmsDbContext _db;

    public CurrentUser(IHttpContextAccessor http, SecmsDbContext db)
    {
        _http = http;
        _db = db;
    }

    public string? UserId => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => _http.HttpContext?.User?.Identity?.Name;
    public bool IsAdmin => _http.HttpContext?.User?.IsInRole(AppRoles.Admin) == true;
    public bool IsTeacher => _http.HttpContext?.User?.IsInRole(AppRoles.Teacher) == true;

    public Guid? TeacherProfileId
    {
        get
        {
            var claim = _http.HttpContext?.User?.FindFirstValue("teacherProfileId");
            if (Guid.TryParse(claim, out var id)) return id;
            return null;
        }
    }
}

public class AuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly JwtOptions _jwt;
    private readonly SecmsDbContext _db;

    public AuthService(UserManager<AppUser> users, IOptions<JwtOptions> jwt, SecmsDbContext db)
    {
        _users = users;
        _jwt = jwt.Value;
        _db = db;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByNameAsync(request.UserName);
        if (user == null || !user.IsActive) return null;
        if (!await _users.CheckPasswordAsync(user, request.Password)) return null;

        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault();
        if (string.IsNullOrEmpty(role)) return null;
        Guid? teacherId = user.TeacherProfileId;
        if (teacherId == null && role == AppRoles.Teacher)
        {
            teacherId = await _db.TeacherProfiles.Where(t => t.UserId == user.Id).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        }

        var token = CreateToken(user, role, teacherId);
        return new LoginResponse(token, _jwt.AccessTokenMinutes * 60, role, user.DisplayName, user.Id, teacherId);
    }

    public async Task<MeResponse?> MeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return null;
        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "";
        var teacherId = user.TeacherProfileId ?? await _db.TeacherProfiles.Where(t => t.UserId == user.Id).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        return new MeResponse(user.Id, user.UserName ?? "", user.Email ?? "", role, user.DisplayName, teacherId);
    }

    private string CreateToken(AppUser user, string role, Guid? teacherProfileId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Role, role),
            new("displayName", user.DisplayName)
        };
        if (teacherProfileId.HasValue)
            claims.Add(new Claim("teacherProfileId", teacherProfileId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
