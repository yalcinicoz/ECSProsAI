using ECSPros.Order.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Infrastructure.Services;

/// <summary>Core şemasından salt-okunur kanal/firma/sözleşme bilgisi (raw SQL; OP2 + FE0).</summary>
public class FirmResolver(OrderDbContext db) : IFirmResolver
{
    private sealed class FirmRow { public Guid FirmId { get; set; } }

    private sealed class ChannelRow
    {
        public Guid Id { get; set; }
        public Guid FirmId { get; set; }
        public string Code { get; set; } = "";
        public string? Name { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class ContractRow
    {
        public Guid Id { get; set; }
        public Guid FirmId { get; set; }
        public Guid? FirmPlatformId { get; set; }
        public string ServiceCode { get; set; } = "";
        public string ServiceType { get; set; } = "";
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; } = "";
    }

    public async Task<Guid?> GetFirmIdAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var satirlar = await db.Database.SqlQuery<FirmRow>($"""
            SELECT "FirmId" FROM core.core_firm_platforms
            WHERE "Id" = {firmPlatformId} AND "IsDeleted" = false
            """).ToListAsync(ct);
        return satirlar.Count == 0 ? null : satirlar[0].FirmId;
    }

    public async Task<ChannelInfo?> GetChannelAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<ChannelRow>($"""
            SELECT "Id", "FirmId", "Code", COALESCE("NameI18n"->>'tr', "NameI18n"->>'en', "Code") AS "Name", "IsActive"
            FROM core.core_firm_platforms
            WHERE "Id" = {firmPlatformId} AND "IsDeleted" = false
            """).ToListAsync(ct);
        return rows.Count == 0 ? null : Map(rows[0]);
    }

    public async Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<ChannelRow>($"""
            SELECT "Id", "FirmId", "Code", COALESCE("NameI18n"->>'tr', "NameI18n"->>'en', "Code") AS "Name", "IsActive"
            FROM core.core_firm_platforms
            WHERE "IsDeleted" = false
            ORDER BY "Code"
            """).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<IntegrationContractInfo>> GetEInvoiceContractsAsync(Guid? firmId = null, CancellationToken ct = default)
    {
        // Not: null Guid? parametresi "42P18 could not determine data type" verir (2026-09-06 canlı hata) —
        // firma süzgeci iki ayrı sorguyla verilir, null parametre hiç gönderilmez.
        var query = firmId is null
            ? db.Database.SqlQuery<ContractRow>($"""
                SELECT i."Id", i."FirmId", i."FirmPlatformId", s."Code" AS "ServiceCode", s."ServiceType",
                       i."Name", i."IsActive", i."Status"
                FROM core.core_firm_platform_integrations i
                JOIN definition.integration_services s ON s."Id" = i."IntegrationServiceId"
                WHERE i."IsDeleted" = false AND s."ServiceType" = 'einvoice'
                ORDER BY i."Name"
                """)
            : db.Database.SqlQuery<ContractRow>($"""
                SELECT i."Id", i."FirmId", i."FirmPlatformId", s."Code" AS "ServiceCode", s."ServiceType",
                       i."Name", i."IsActive", i."Status"
                FROM core.core_firm_platform_integrations i
                JOIN definition.integration_services s ON s."Id" = i."IntegrationServiceId"
                WHERE i."IsDeleted" = false AND s."ServiceType" = 'einvoice' AND i."FirmId" = {firmId.Value}
                ORDER BY i."Name"
                """);
        var rows = await query.ToListAsync(ct);
        return rows.Select(r => new IntegrationContractInfo(
            r.Id, r.FirmId, r.FirmPlatformId, r.ServiceCode, r.ServiceType, r.Name, r.IsActive, r.Status)).ToList();
    }

    private static ChannelInfo Map(ChannelRow r) => new(r.Id, r.FirmId, r.Code, r.Name ?? r.Code, r.IsActive);
}
