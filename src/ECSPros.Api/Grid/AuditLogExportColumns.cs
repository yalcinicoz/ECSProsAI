using ECSPros.Iam.Application.Queries.GetAuditLogs;

namespace ECSPros.Api.Grid;

/// <summary>Denetim logları Excel kolonları — anahtarlar panel DataGrid kolon anahtarlarıyla aynı; tarih kilitli.</summary>
public static class AuditLogExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<AuditLogExportRow>> All = new GridExportColumn<AuditLogExportRow>[]
    {
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt), Locked: true),
        new("action", "İşlem", r => r.Action),
        new("entityType", "Kayıt Tipi", r => r.EntityType),
        new("entityId", "Kayıt", r => r.EntityId.ToString()),
        new("user", "Kullanıcı", r => r.UserName),
        new("userId", "Kullanıcı Id", r => r.UserId?.ToString()),
        new("ip", "IP", r => r.IpAddress),
        new("userAgent", "Tarayıcı", r => r.UserAgent),
    };
}
