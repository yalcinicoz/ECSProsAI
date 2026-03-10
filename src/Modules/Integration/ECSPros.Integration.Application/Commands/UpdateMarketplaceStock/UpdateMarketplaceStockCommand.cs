using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.UpdateMarketplaceStock;

public record UpdateMarketplaceStockCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    Guid VariantId,
    int Quantity) : IRequest<Result<bool>>;

public class UpdateMarketplaceStockCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<UpdateMarketplaceStockCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateMarketplaceStockCommand request, CancellationToken ct)
    {
        var mp = db.MarketplaceProducts
            .FirstOrDefault(x => x.FirmIntegrationId == request.FirmIntegrationId && x.VariantId == request.VariantId);

        if (mp is null)
            return Result.Failure<bool>("Pazaryerinde eşleşen ürün bulunamadı.");

        var adapter = adapterResolver.GetMarketplaceAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await adapter.UpdateStockAsync(request.FirmIntegrationId, mp.ExternalId, request.Quantity, ct);
        sw.Stop();

        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "marketplace",
            OperationType = "update_stock",
            Status = result.Success ? "success" : "failure",
            ErrorMessage = result.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ReferenceId = request.VariantId,
            ReferenceType = "Variant"
        });

        if (result.Success)
        {
            mp.MarketplaceStock = request.Quantity;
            mp.StockSyncedAt = DateTime.UtcNow;
            mp.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return result.Success ? Result.Success(true) : Result.Failure<bool>(result.ErrorMessage ?? "Stok güncelleme başarısız.");
    }
}
