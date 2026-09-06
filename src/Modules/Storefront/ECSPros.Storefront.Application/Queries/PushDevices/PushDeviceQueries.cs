using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.PushDevices;

/// <summary>Token TAM haliyle yalnız bildirim gönderim yüzeyine verilir; üye/panel
/// listelerinde maskeli kuyruk (TokenTail) döner — token fiilen gönderim adresidir.</summary>
public record PushDeviceDto(
    Guid Id, Guid FirmPlatformId, Guid? MemberId, string Platform, string TokenTail,
    string? DeviceIdentifier, string? AppVersion, string Status,
    DateTime LastSeenAt, DateTime CreatedAt);

public record PushDeviceAdminDto(
    Guid Id, Guid FirmPlatformId, Guid? MemberId, string Platform, string Token,
    string? DeviceIdentifier, string? AppVersion, string Status,
    DateTime LastSeenAt, DateTime CreatedAt);

/// <summary>Üyenin kayıtlı cihazları (mobil "cihazlarım" + panel üye detayı).</summary>
public record GetMemberPushDevicesQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<PushDeviceDto>>>;

public class GetMemberPushDevicesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberPushDevicesQuery, Result<List<PushDeviceDto>>>
{
    public async Task<Result<List<PushDeviceDto>>> Handle(GetMemberPushDevicesQuery request, CancellationToken ct)
        => Result.Success(await db.PushDevices.AsNoTracking()
            .Where(d => d.FirmPlatformId == request.FirmPlatformId && d.MemberId == request.MemberId)
            .OrderByDescending(d => d.LastSeenAt)
            .Take(50)
            .Select(d => new PushDeviceDto(d.Id, d.FirmPlatformId, d.MemberId, d.Platform,
                d.Token.Length <= 8 ? d.Token : "…" + d.Token.Substring(d.Token.Length - 8),
                d.DeviceIdentifier, d.AppVersion, d.Status, d.LastSeenAt, d.CreatedAt))
            .ToListAsync(ct));
}

/// <summary>Bildirim gönderim/izleme yüzeyi (admin): filtreli + sayfalı, token TAM döner.</summary>
public record GetPushDevicesForAdminQuery(
    Guid? FirmPlatformId = null, Guid? MemberId = null, string? Status = null,
    int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<PushDeviceAdminDto>>>;

public class GetPushDevicesForAdminQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetPushDevicesForAdminQuery, Result<PagedResult<PushDeviceAdminDto>>>
{
    public async Task<Result<PagedResult<PushDeviceAdminDto>>> Handle(GetPushDevicesForAdminQuery request, CancellationToken ct)
    {
        var q = db.PushDevices.AsNoTracking().AsQueryable();
        if (request.FirmPlatformId is { } fp) q = q.Where(x => x.FirmPlatformId == fp);
        if (request.MemberId is { } m) q = q.Where(x => x.MemberId == m);
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(x => x.Status == request.Status);

        var sayfa = Math.Max(1, request.Page);
        var boy = Math.Clamp(request.PageSize, 1, 200);
        var toplam = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.LastSeenAt)
            .Skip((sayfa - 1) * boy).Take(boy)
            .Select(d => new PushDeviceAdminDto(d.Id, d.FirmPlatformId, d.MemberId, d.Platform,
                d.Token, d.DeviceIdentifier, d.AppVersion, d.Status, d.LastSeenAt, d.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<PushDeviceAdminDto>(items, toplam, sayfa, boy));
    }
}
