using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Secms.Application.Interfaces;
using Secms.Application.Options;
using Secms.Infrastructure.Identity;
using Secms.Infrastructure.Persistence;
using Secms.Infrastructure.Services;
using Secms.Infrastructure.Storage;

namespace Secms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<SchedulingOptions>(config.GetSection(SchedulingOptions.SectionName));
        services.Configure<AttendanceOptions>(config.GetSection(AttendanceOptions.SectionName));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));

        services.AddDbContext<SecmsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddIdentity<AppUser, IdentityRole>(opt =>
            {
                opt.Password.RequiredLength = 6;
                opt.Password.RequireNonAlphanumeric = false;
                opt.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<SecmsDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(opt =>
        {
            opt.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            opt.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<AuthService>();
        services.AddScoped<FacilityService>();
        services.AddScoped<TeacherService>();
        services.AddScoped<StudentService>();
        services.AddScoped<ClassGroupService>();
        services.AddScoped<SchedulingService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<BillingService>();

        return services;
    }
}
