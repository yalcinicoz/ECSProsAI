using ECSPros.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/migration")]
[Authorize]
public class MigrationController : ControllerBase
{
    private readonly MigrationService _migrationService;

    public MigrationController(MigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var state = _migrationService.GetState();
        var stats = await _migrationService.GetTableStatsAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                status = state.Status.ToString().ToLower(),
                phase = state.Phase,
                startedAt = state.StartedAt,
                finishedAt = state.FinishedAt,
                error = state.Error,
                output = state.Output,
                tableStats = stats
            }
        });
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunMigrationRequest req)
    {
        int phase = req?.Phase ?? 0;
        if (phase < 0 || phase > 9)
            return BadRequest(new { success = false, error = "Geçersiz faz. 0-9 arasında olmalı." });

        bool started = await _migrationService.StartAsync(phase);
        if (!started)
            return Conflict(new { success = false, error = "Migration zaten çalışıyor." });

        return Ok(new { success = true, data = new { message = $"Faz {(phase == 0 ? "Tümü" : phase.ToString())} başlatıldı." } });
    }
}

public record RunMigrationRequest(int Phase = 0);
