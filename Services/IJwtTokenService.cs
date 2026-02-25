using Pehlione.Models.Identity;

namespace Pehlione.Services;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc, string[] Roles)> CreateTokenAsync(ApplicationUser user, CancellationToken ct);
}
