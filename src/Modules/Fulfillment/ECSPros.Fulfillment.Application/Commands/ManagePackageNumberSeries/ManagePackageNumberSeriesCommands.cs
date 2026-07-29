using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.ManagePackageNumberSeries;

// ── Listeleme: tüm kanallar + (varsa) paket no serisi ──────────────────────────

public record GetPackageNumberSeriesQuery : IRequest<Result<List<PackageNumberSeriesDto>>>;

public record PackageNumberSeriesDto(
    Guid FirmPlatformId,
    string ChannelCode,
    string? ChannelName,
    bool HasSeries,
    string? Prefix,
    int? PadLength,
    long? NextValue,
    bool? IsActive);

public class GetPackageNumberSeriesQueryHandler
    : IRequestHandler<GetPackageNumberSeriesQuery, Result<List<PackageNumberSeriesDto>>>
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

    private readonly IFulfillmentDbContext _context;
    public GetPackageNumberSeriesQueryHandler(IFulfillmentDbContext context) => _context = context;

    public async Task<Result<List<PackageNumberSeriesDto>>> Handle(
        GetPackageNumberSeriesQuery request, CancellationToken ct)
    {
        var db = (DbContext)_context;
        var rows = await db.Database.SqlQuery<Row>($"""
            SELECT fp."Id" AS "FirmPlatformId",
                   fp."Code" AS "ChannelCode",
                   fp."NameI18n"->>'tr' AS "ChannelName",
                   s."Prefix", s."PadLength", s."NextValue", s."IsActive"
            FROM core.core_firm_platforms fp
            LEFT JOIN fulfillment.ful_package_number_series s
                   ON s."FirmPlatformId" = fp."Id" AND s."IsDeleted" = false
            WHERE fp."IsDeleted" = false
            ORDER BY fp."Code"
            """).ToListAsync(ct);

        var list = rows.Select(r => new PackageNumberSeriesDto(
            r.FirmPlatformId, r.ChannelCode, r.ChannelName,
            r.Prefix is not null, r.Prefix, r.PadLength, r.NextValue, r.IsActive)).ToList();

        return Result.Success(list);
    }
}

// ── Upsert: önek/dolgu/aktiflik — sayaç ASLA elle değiştirilemez ───────────────

public record UpsertPackageNumberSeriesCommand(
    Guid FirmPlatformId,
    string Prefix,
    int PadLength,
    bool IsActive) : IRequest<Result<bool>>;

public class UpsertPackageNumberSeriesCommandHandler
    : IRequestHandler<UpsertPackageNumberSeriesCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;
    public UpsertPackageNumberSeriesCommandHandler(IFulfillmentDbContext context) => _context = context;

    public async Task<Result<bool>> Handle(UpsertPackageNumberSeriesCommand request, CancellationToken ct)
    {
        var prefix = request.Prefix.Trim().ToUpperInvariant();
        if (prefix.Length > 10)
            return Result.Failure<bool>("Önek en fazla 10 karakter olabilir.");
        if (prefix.Length > 0 && !prefix.All(char.IsAsciiLetterOrDigit))
            return Result.Failure<bool>("Önek yalnız harf ve rakam içerebilir.");
        if (request.PadLength is < 4 or > 12)
            return Result.Failure<bool>("Dolgu uzunluğu 4-12 arasında olmalıdır.");

        var seri = await _context.PackageNumberSeries
            .FirstOrDefaultAsync(s => s.FirmPlatformId == request.FirmPlatformId, ct);

        if (seri is null)
        {
            _context.PackageNumberSeries.Add(new PackageNumberSeries
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
