using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Integration.Application.Commands.FetchMarketplaceOrders;

public record FetchMarketplaceOrdersCommand(
    Guid FirmIntegrationId,
    string ServiceCode,
    DateTime? Since = null) : IRequest<Result<FetchOrdersResult>>;

public record FetchOrdersResult(int FetchedCount, List<MarketplaceOrderDto> Orders);

public class FetchMarketplaceOrdersCommandHandler(
    IIntegrationDbContext db,
    IAdapterResolver adapterResolver)
    : IRequestHandler<FetchMarketplaceOrdersCommand, Result<FetchOrdersResult>>
{
    public async Task<Result<FetchOrdersResult>> Handle(FetchMarketplaceOrdersCommand request, CancellationToken ct)
    {
        var adapter = adapterResolver.GetMarketplaceAdapter(request.ServiceCode);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<MarketplaceOrderDto> orders;
        string? error = null;
        try
        {
            orders = await adapter.FetchOrdersAsync(request.FirmIntegrationId, request.Since, ct);
        }
        catch (Exception ex)
        {
            orders = [];
            error = ex.Message;
        }
        sw.Stop();

        db.IntegrationLogs.Add(new IntegrationLog
        {
            FirmIntegrationId = request.FirmIntegrationId,
            ServiceType = "marketplace",
            OperationType = "fetch_orders",
            Status = error == null ? "success" : "failure",
            ErrorMessage = error,
            DurationMs = (int)sw.ElapsedMilliseconds
        });
        await db.SaveChangesAsync(ct);

        if (error != null)
            return Result.Failure<FetchOrdersResult>(error);

        return Result.Success(new FetchOrdersResult(orders.Count, orders.ToList()));
    }
}
