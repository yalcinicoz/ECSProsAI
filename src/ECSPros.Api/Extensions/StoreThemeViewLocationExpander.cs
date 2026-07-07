using Microsoft.AspNetCore.Mvc.Razor;

namespace ECSPros.Api.Extensions;

/// <summary>
/// Storefront tema çözümleyicisi (plan 3.5 / A11).
///
/// Varsayılan tema "misharix" doğrudan ~/Views/ ağacında yaşar — kaynak tasarım
/// projesindeki (/opt/misharixWebSites) partial'lar "~/Views/..." mutlak yollarıyla
/// birbirine bağlı olduğundan, dosyalar bayt-bayt aynı kalabilsin diye varsayılan
/// tema kök ağaca yerleştirilir. Farklı tema seçen bir platform için view'lar önce
/// ~/Views/Themes/{tema}/ altında aranır, bulunamazsa kök ağaca (misharix) düşülür.
/// Tema kodu şimdilik sabittir; FirmPlatform.ThemeCode bağlaması A12/B fazında
/// istekten çözülecek şekilde PopulateValues içine eklenecektir.
/// </summary>
public sealed class StoreThemeViewLocationExpander : IViewLocationExpander
{
    public const string DefaultTheme = "misharix";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        context.Values["ms-theme"] = DefaultTheme;
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (context.Values.TryGetValue("ms-theme", out var theme)
            && !string.IsNullOrEmpty(theme)
            && !string.Equals(theme, DefaultTheme, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var location in viewLocations)
                yield return location.Replace("/Views/", $"/Views/Themes/{theme}/");
        }

        foreach (var location in viewLocations)
            yield return location;
    }
}
