using ECSPros.Core.Application.Queries.GetIntegrationServices;
using ECSPros.Core.Application.Queries.GetPlatformTypes;

namespace ECSPros.Api.Grid;

/// <summary>Platform tipleri Excel kolonları (kod kilitli).</summary>
public static class PlatformTypeExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<PlatformTypeExportRow>> All = new GridExportColumn<PlatformTypeExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("isMarketplace", "Pazaryeri", r => r.IsMarketplace ? "Evet" : "Hayır"),
        new("channelCount", "Kanal", r => r.ChannelCount),
        new("hasSchema", "Ayar Şeması", r => r.HasSchema ? "Var" : "—"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}

/// <summary>Servis kataloğu Excel kolonları (kod kilitli).</summary>
public static class IntegrationServiceExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<IntegrationServiceExportRow>> All = new GridExportColumn<IntegrationServiceExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("serviceType", "Servis Tipi", r => r.ServiceType),
        new("isAvailable", "Kullanılabilir", r => r.IsAvailable ? "Evet" : "Hayır"),
        new("integrationCount", "Firma Entegrasyonu", r => r.IntegrationCount),
        new("hasSchema", "Ayar Şeması", r => r.HasSchema ? "Var" : "—"),
        new("cargoCodeStrategy", "Kargo Kod Stratejisi", r => r.CargoCodeStrategy),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
