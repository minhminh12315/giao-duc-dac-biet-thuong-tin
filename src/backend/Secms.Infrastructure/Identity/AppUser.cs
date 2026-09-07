using Microsoft.AspNetCore.Identity;

namespace Secms.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? TeacherProfileId { get; set; }
}
