using Microsoft.AspNetCore.Identity;
using Pehlione.Models;

namespace Pehlione.Models.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
}
