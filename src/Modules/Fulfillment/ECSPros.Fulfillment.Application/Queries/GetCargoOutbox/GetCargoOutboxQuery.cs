using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetCargoOutbox;

/// <summary>OP5: kargo bildirim kuyruğu — yönlendirme ekranı (taşıyıcı bazlı gruplanır)
/// ve gönderim izleme.</summary>
public record GetCargoOutboxQuery(string? Status = null) : IRequest<Result<List<CargoOutboxDto>>>;

public record CargoOutboxDto(
    Guid Id, Guid PackageId, string PackageNumber, Guid OrderId,
    Guid? CargoIntegrationId, string? CargoName, string Status,
    int AttemptCount, string? LastError, DateTime? SentAt, DateTime CreatedAt);

public class GetCargoOutboxQueryHandler(IFulfillmentDbContext db)
    : IRequestHandler<GetCargoOutboxQuery, Result<List<CargoOutboxDto>>>
{
    public async Task<Result<List<CargoOutboxDto>>> Handle(GetCargoOutboxQuery request, CancellationToken ct)
    {
        var sorgu = db.CargoNotifyOutbox.AsNoTracking();
        if (request.Status is { Length: > 0 } s) sorgu = sorgu.Where(o => o.Status == s);

        var liste = await (from o in sorgu
                           join p in db.Packages.AsNoTracking() on o.PackageId equals p.Id
                           orderby o.CreatedAt descending
                           select new CargoOutboxDto(o.Id, o.PackageId, p.PackageNumber, o.OrderId,
                               o.CargoIntegrationId, o.CargoName, o.Status,
                               o.AttemptCount, o.LastError, o.SentAt, o.CreatedAt))
            .Take(500)
            .ToListAsync(ct);
        return Result.Success(liste);
    }
}
