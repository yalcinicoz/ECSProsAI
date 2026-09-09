using ECSPros.Core.Application.Queries.GetUiTranslations;

namespace ECSPros.Api.Grid;

/// <summary>Arayüz çevirileri Excel kolonları — DÜZ biçim (anahtar × dil), çevirmene gönderilebilir.</summary>
public static class UiTranslationExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<UiTranslationExportRow>> All = new GridExportColumn<UiTranslationExportRow>[]
    {
        new("namespace", "Grup", r => r.Namespace, Locked: true),
        new("key", "Anahtar", r => r.Key, Locked: true),
        new("lang", "Dil", r => r.Lang),
        new("value", "Değer", r => r.Value),
        new("guncelleme", "Güncelleme", r => r.Guncelleme),
    };
}
