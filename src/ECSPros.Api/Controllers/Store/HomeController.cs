using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Storefront ana sayfası (Razor). Sayfa controller'ları iş mantığını
/// api/store/* ile aynı MediatR handler'ları üzerinden çağırır (plan 3.4).
/// </summary>
public class HomeController : StorePageController
{
    [HttpGet("/")]
    public IActionResult Index() => View();
}
