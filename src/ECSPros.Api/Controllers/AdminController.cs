using ECSPros.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IServiceProvider _sp;
    private readonly IWebHostEnvironment _env;

    public AdminController(IServiceProvider sp, IWebHostEnvironment env)
    {
        _sp  = sp;
        _env = env;
    }

    /// <summary>
    /// İş verilerini temizler ve demo seed verilerini yükler.
    /// Sadece Development ortamında çalışır.
    /// </summary>
    [HttpPost("seed/reset-demo")]
    public async Task<IActionResult> ResetDemoData()
    {
        if (!_env.IsDevelopment())
            return StatusCode(403, new { success = false, error = "Bu endpoint sadece Development ortamında kullanılabilir." });

        try
        {
            await TestDataSeeder.ResetAndSeedAsync(_sp);
            return Ok(new { success = true, message = "Demo veriler başarıyla yüklendi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
