using Microsoft.EntityFrameworkCore;

namespace Pehlione.Data;

public sealed class PehlioneDbContext : DbContext
{
    public PehlioneDbContext(DbContextOptions<PehlioneDbContext> options)
        : base(options)
    {
    }

    // Simdilik DbSet yok: once baglanti testi yapiliyor.
}
