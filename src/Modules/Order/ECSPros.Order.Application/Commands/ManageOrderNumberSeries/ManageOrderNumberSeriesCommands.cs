using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ManageOrderNumberSeries;

// ── Listeleme: tüm kanallar + (varsa) seri bilgisi ─────────────────────────────

public record GetOrderNumberSeriesQuery : IRequest<Result<List<OrderNumberSeriesDto>>>;

public record OrderNumberSeriesDto(
    Guid FirmPlatformId,
    string ChannelCode,
    string? ChannelName,
    bool HasSeries,
    string? Prefix,
    int? PadLength,
    long? NextValue,
    bool? IsActive);

public class GetOrderNumberSeriesQueryHandler
    : IRequestHandler<GetOrderNumberSeriesQuery, Result<List<OrderNumberSeriesDto>>>
{
    private sealed class Row
    {
        public Guid FirmPlatformId { get; set; }
        public string ChannelCode { get; set; } = string.Empty;
        public string? ChannelName { get; set; }
        public string? Prefix { get; set; }
        public int? PadLength { get; set; }
        public long? NextValue { get; set; }
        public bool? IsActive { get; set; }
    }

    private readonly IOrderDbContext _context;
    public GetOrderNumberSeriesQueryHandler(IOrderDbContext context) => _context = context;

    public async Task<Result<List<OrderNumberSeriesDto>>> Handle(
        GetOrderNumberSeriesQuery request, CancellationToken ct)
    {
        // Kanal listesi core şemasından (salt-okunur) — seri satırı olmayan kanallar da görünür
        var db = (DbContext)_context;
        var rows = await db.Database.SqlQuery<Row>($"""
            SELECT fp."Id" AS "FirmPlatformId",
                   fp."Code" AS "ChannelCode",
                   fp."NameI18n"->>'tr' AS "ChannelName",
                   s."Prefix", s."PadLength", s."NextValue", s."IsActive"
            FROM core.core_firm_platforms fp
            LEFT JOIN "order".ord_order_number_series s
                   ON s."FirmPlatformId" = fp."Id" AND s."IsDeleted" = false
            WHERE fp."IsDeleted" = false
            ORDER BY fp."Code"
            """).ToListAsync(ct);

        var list = rows.Select(r => new OrderNumberSeriesDto(
            r.FirmPlatformId, r.ChannelCode, r.ChannelName,
            r.Prefix is not null, r.Prefix, r.PadLength, r.NextValue, r.IsActive)).ToList();

        return Result.Success(list);
    }
}

// ── Upsert: önek/dolgu/aktiflik — sayaç ASLA elle değiştirilemez ───────────────

public record UpsertOrderNumberSeriesCommand(
    Guid FirmPlatformId,
    string Prefix,
    int PadLength,
    bool IsActive) : IRequest<Result<bool>>;

public class UpsertOrderNumberSeriesCommandHandler
    : IRequestHandler<UpsertOrderNumberSeriesCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;
    public UpsertOrderNumberSeriesCommandHandler(IOrderDbContext context) => _context = context;

    public async Task<Result<bool>> Handle(UpsertOrderNumberSeriesCommand request, CancellationToken ct)
    {
        var prefix = request.Prefix.Trim().ToUpperInvariant();
        if (prefix.Length > 10)
            return Result.Failure<bool>("Önek en fazla 10 karakter olabilir.");
        if (prefix.Length > 0 && !prefix.All(char.IsAsciiLetterOrDigit))
            return Result.Failure<bool>("Önek yalnız harf ve rakam içerebilir.");
        if (request.PadLength is < 4 or > 12)
            return Result.Failure<bool>("Dolgu uzunluğu 4-12 arasında olmalıdır.");

        var seri = await _context.OrderNumberSeries
            .FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId, ct);

        if (seri is null)
        {
            _context.OrderNumberSeries.Add(new OrderNumberSeries
            {
                FirmPlatformId = request.FirmPlatformId,
                Prefix = prefix,
                PadLength = request.PadLength,
                NextValue = 1,
                IsActive = request.IsActive
            });
        }
        else
        {
            seri.Prefix = prefix;
            seri.PadLength = request.PadLength;
            seri.IsActive = request.IsActive;
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
