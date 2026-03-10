using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSupplierTransactions;

public record GetSupplierTransactionsQuery(
    Guid SupplierId,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<SupplierTransactionDto>>>;

public record SupplierTransactionDto(
    Guid Id,
    string TransactionType,
    decimal Debit,
    decimal Credit,
    decimal BalanceAfter,
    string? ReferenceType,
    Guid? ReferenceId,
    string? Description,
    DateOnly TransactionDate,
    DateTime CreatedAt);

public class GetSupplierTransactionsQueryHandler : IRequestHandler<GetSupplierTransactionsQuery, Result<PagedResult<SupplierTransactionDto>>>
{
    private readonly IFinanceDbContext _db;

    public GetSupplierTransactionsQueryHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierTransactionDto>>> Handle(GetSupplierTransactionsQuery request, CancellationToken ct)
    {
        var query = _db.SupplierTransactions.Where(t => t.SupplierId == request.SupplierId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new SupplierTransactionDto(
                t.Id, t.TransactionType, t.Debit, t.Credit, t.BalanceAfter,
                t.ReferenceType, t.ReferenceId, t.Description, t.TransactionDate, t.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SupplierTransactionDto>(items, total, request.Page, request.PageSize));
    }
}
