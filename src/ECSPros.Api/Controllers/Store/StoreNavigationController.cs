using ECSPros.Api.Models.Store;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// İlk sayfa HTML'ini büyütmemek için etkileşim anında getirilen storefront
/// navigasyon parçalarını sunar.
/// </summary>
[Route("store/navigation")]
public sealed class StoreNavigationController : StorePageController
{
    [HttpGet("mega-menu")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByHeader = "Host")]
    public IActionResult MegaMenu()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        return PartialView(
            "~/Views/ProjeElementleri/Navigasyon/_AnaNavigasyonMegaMenu.cshtml",
            nav);
    }
}
