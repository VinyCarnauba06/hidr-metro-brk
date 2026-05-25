using HidrometroApp.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace HidrometroApp.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly HidrometroDbContext _db;

    public HealthController(HidrometroDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        try
        {
            await _db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow, database = "ok" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", database = "error", detail = ex.Message });
        }
    }
}
