using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pehlione.Models.Identity;

namespace Pehlione.Data;

public sealed class PehlioneDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    // Ileride e-ticaret domain DbSet'leri buraya gelecek.
}
