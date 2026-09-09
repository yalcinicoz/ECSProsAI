using Npgsql;

namespace ECSPros.Api.Services;

/// <summary>
/// Müşteri İlişkileri yeni kayıt formu: sipariş numarasından sipariş + müşteri anlık görüntüsü (eski
/// MusteriIliskileriYonetimiSiparisBilgileri). Numara bizim OrderNumber, ExternalOrderNumber ya da eski sayısal
/// LegacyOrderId olabilir. Aynı numara birden fazla siparişte (farklı kanal) → tümü döner, panel kanal seçtirir (K9).
/// Ham SQL: order + core + crm şemaları (modül sınırı; salt okuma).
/// </summary>
public sealed class CrmTicketOrderLookup(NpgsqlDataSource dataSource)
{
    public sealed record Aday(Guid OrderId, string OrderNumber, Guid FirmPlatformId, string PlatformName, DateTime CreatedAt,
        decimal GrandTotal, string Status, Guid? MemberId, int? LegacyMemberId, string CustomerName, string CustomerPhone);

    public async Task<List<Aday>> BulAsync(string number, CancellationToken ct)
    {
        var n = number.Trim();
        if (n.Length == 0) return [];
        int.TryParse(n, out var legacyId);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT o."Id", o."OrderNumber", o."FirmPlatformId", COALESCE(p."NameI18n"->>'tr', p."Code", ''), o."CreatedAt", o."GrandTotal", o."Status",
                   o."MemberId", m."LegacyMemberId",
                   COALESCE(NULLIF(TRIM(m."FirstName" || ' ' || m."LastName"), ''), o."ShippingRecipientName", ''),
                   COALESCE(m."Phone", o."ShippingRecipientPhone", '')
              FROM "order".ord_orders o
              LEFT JOIN core.core_firm_platforms p ON p."Id" = o."FirmPlatformId"
              LEFT JOIN crm.crm_members m ON m."Id" = o."MemberId" AND NOT m."IsDeleted"
             WHERE NOT o."IsDeleted" AND (o."OrderNumber" = @n OR o."ExternalOrderNumber" = @n OR (@legacy > 0 AND o."LegacyOrderId" = @legacy))
             ORDER BY o."CreatedAt" DESC LIMIT 10
            """, conn);
        cmd.Parameters.AddWithValue("n", n);
        cmd.Parameters.AddWithValue("legacy", legacyId);
        var list = new List<Aday>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new Aday(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetString(3), r.GetDateTime(4), r.GetDecimal(5), r.GetString(6),
                r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetInt32(8), r.GetString(9), r.GetString(10)));
        return list;
    }
}
