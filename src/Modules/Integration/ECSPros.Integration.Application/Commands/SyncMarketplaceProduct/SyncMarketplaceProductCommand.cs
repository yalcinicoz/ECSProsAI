using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.SyncMarketplaceProduct;

public record SyncMarketplaceProductCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    MarketplaceProductPayload Payload) : IRequest<Result<SyncMarketplaceProductResult>>;

public record SyncMarketplaceProductResult(Guid MarketplaceProductId, string ExternalId, string SyncStatus);

public class SyncMarketplaceProductCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<SyncMarketplaceProductCommand, Result<SyncMarketplaceProductResult>>
{
    public async Task<Result<SyncMarketplaceProductResult>> Handle(
        SyncMarketplaceProductCommand request, CancellationToken ct)
    {
        var adapter = adapterResolver.GetMarketplaceAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var syncResult = await adapter.SyncProductAsync(request.FirmIntegrationId, request.Payload, ct);
        sw.Stop();

        var log = new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "marketplace",
            OperationType = "sync_product",
            Status = syncResult.Success ? "success" : "failure",
            ErrorMessage = syncResult.ErrorMessage,
            DurationMs = (int)sw.ElapsedMilliseconds,
            ReferenceId = request.Payload.VariantId,
            ReferenceType = "Variant"
        };
        db.IntegrationLogs.Add(log);

        if (!syncResult.Success)
        {
            await db.SaveChangesAsync(ct);
            return Result.Failure<SyncMarketplaceProductResult>(syncResult.ErrorMessage ?? "Senkronizasyon başarısız.");
        }

        var existing = db.MarketplaceProducts
            .FirstOrDefault(x => x.FirmIntegrationId == request.FirmIntegrationId && x.VariantId == request.Payload.VariantId);

        if (existing is null)
        {
            existing = new MarketplaceProduct
            {
                FirmIntegrationId = request.FirmIntegrationId,
                VariantId = request.Payload.VariantId,
                ExternalId = syncResult.ExternalId!,
                ExternalBarcode = request.Payload.Barcode,
                SyncStatus = "synced",
                LastSyncedAt = DateTime.UtcNow,
                MarketplacePrice = request.Payload.Price,
                MarketplaceStock = request.Payload.StockQuantity
            };
            db.MarketplaceProducts.Add(existing);
        }
        else
        {
            existing.ExternalId = syncResult.ExternalId!;
            existing.SyncStatus = "synced";
            existing.LastSyncedAt = DateTime.UtcNow;
            existing.MarketplacePrice = request.Payload.Price;
            existing.MarketplaceStock = request.Payload.StockQuantity;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new SyncMarketplaceProductResult(existing.Id, existing.ExternalId, existing.SyncStatus));
    }
}
