using Microsoft.AspNetCore.Identity;

namespace Carpediem.Modules.Identity.Domain;

public sealed class CarpediemUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
}
