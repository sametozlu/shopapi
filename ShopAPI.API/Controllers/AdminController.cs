using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopAPI.Domain;
using ShopAPI.Infrastructure;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AdminController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int take = 50)
    {
        var logs = await _dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Min(take, 200))
            .ToListAsync();
        return Ok(logs);
    }
}
