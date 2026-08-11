using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Queries.GetSupplierSettlements;

/// <summary>P3a (2026-08-11): satıcının hakediş satırları — katman izli (K1 "oran neden %X"
/// sorusu satırdan okunur). Status filtresi: pending | available | paid | reversed.</summary>
public record GetSupplierSettlementsQuery(
    Guid SupplierAccountId,
    string? Status = null,
    DateTime? Since = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<SettlementLineDto>>>;

public record SettlementLineDto(
    Guid Id,
    string OrderNumber,
    string Sku,
    string ProductName,
    int Quantity,
    decimal GrossAmount,
    decimal CommissionRate,
    string CommissionLayer,
    decimal CommissionAmount,
    decimal CampaignDiscountShareAmount,
    decimal NetAmount,
    string Status,
    DateTime DeliveredAt,
    DateTime EligibleAt,
    DateTime? AvailableAt,
    DateTime? PaidAt,
    bool IsReversal);

/// <summary>Hakediş defteri özeti + hareketleri (bakiye 'hakedis' ledger'ından).</summary>
public record GetSupplierStatementQuery(Guid SupplierAccountId, int Page = 1, int PageSize = 50)
    : IRequest<Result<SupplierStatementDto>>;

public record SupplierStatementDto(
    decimal Balance,
    string Currency,
    List<StatementEntryDto> Entries,
    int TotalCount,
    int Page,
    int PageSize);

public record StatementEntryDto(
    DateTime Date,
    string TransactionType,
    decimal Debit,
    decimal Credit,
    decimal BalanceAfter,
    string? Description);

public class SupplierSettlementQueryHandlers(IAccountsDbContext db) :
    IRequestHandler<GetSupplierSettlementsQuery, Result<PagedResult<SettlementLineDto>>>,
    IRequestHandler<GetSupplierStatementQuery, Result<SupplierStatementDto>>
{
    public async Task<Result<PagedResult<SettlementLineDto>>> Handle(GetSupplierSettlementsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var q = db.SettlementLines.AsNoTracking()
            .Where(l => l.SupplierAccountId == request.SupplierAccountId);
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(l => l.Status == request.Status);
        if (request.Since is { } since)
            q = q.Where(l => l.DeliveredAt >= since);

        var total = await q.CountAsync(ct);
        var satirlar = await q.OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new SettlementLineDto(
                l.Id, l.OrderNumber, l.Sku, l.ProductName, l.Quantity,
                l.GrossAmount, l.CommissionRate, l.CommissionLayer, l.CommissionAmount,
                l.CampaignDiscountShareAmount, l.NetAmount, l.Status,
                l.DeliveredAt, l.EligibleAt, l.AvailableAt, l.PaidAt, l.ReversalOfId != null))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SettlementLineDto>(satirlar, total, page, pageSize));
    }

    public async Task<Result<SupplierStatementDto>> Handle(GetSupplierStatementQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var ledger = await db.AccountLedgers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.CurrentAccountId == request.SupplierAccountId
                && l.ConceptCode == "hakedis", ct);
        if (ledger is null)
            return Result.Success(new SupplierStatementDto(0, "TRY", [], 0, page, pageSize));

        var q = db.AccountTransactions.AsNoTracking().Where(t => t.LedgerId == ledger.Id);
        var total = await q.CountAsync(ct);
        var hareketler = await q.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new StatementEntryDto(
                t.CreatedAt, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter, t.Description))
            .ToListAsync(ct);

        return Result.Success(new SupplierStatementDto(
            ledger.Balance, ledger.Currency, hareketler, total, page, pageSize));
    }
}
