using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pehlione.Data;

namespace Pehlione.Controllers;

[ApiController]
[Route("db")]
public sealed class DbPingController : ControllerBase
{
    private readonly PehlioneDbContext _db;

    public DbPingController(PehlioneDbContext db)
    {
        _db = db;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);

            string? provider = _db.Database.ProviderName;
            string? serverVersion = null;

            if (canConnect)
            {
                await using var conn = _db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync(ct);
                }

                serverVersion = conn.ServerVersion;
            }

            return Ok(new
            {
                canConnect,
                provider,
                serverVersion
            });
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Database connection failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
