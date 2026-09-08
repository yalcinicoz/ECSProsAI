using ECSPros.Integration.Application.Queries.GetIntegrationLogs;

namespace ECSPros.Api.Grid;

/// <summary>Entegrasyon logları Excel kolonları (tarih kilitli).</summary>
public static class IntegrationLogExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<IntegrationLogExportRow>> All = new GridExportColumn<IntegrationLogExportRow>[]
    {
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt), Locked: true),
        new("service", "Servis", r => r.ServiceType),
        new("operation", "İşlem", r => r.OperationType),
        new("status", "Durum", r => IntegrationLogGrid.StatusLabel(r.Status)),
        new("duration", "Süre (ms)", r => r.DurationMs),
        new("httpStatus", "HTTP", r => r.HttpStatusCode),
        new("error", "Hata", r => r.ErrorMessage),
        new("referenceType", "Referans Tipi", r => r.ReferenceType),
        new("referenceId", "Referans Id", r => r.ReferenceId?.ToString()),
        new("integrationId", "Entegrasyon Id", r => r.FirmIntegrationId.ToString()),
    };
}
