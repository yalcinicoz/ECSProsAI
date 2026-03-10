using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Core.Application.Commands.CreateExpenseType;

public record CreateExpenseTypeCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsItemLevel,
    decimal DefaultTaxRate,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateExpenseTypeCommandHandler : IRequestHandler<CreateExpenseTypeCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateExpenseTypeCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateExpenseTypeCommand request, CancellationToken ct)
    {
        var expenseType = new ExpenseType
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToLowerInvariant(),
            NameI18n = request.NameI18n,
            IsItemLevel = request.IsItemLevel,
            DefaultTaxRate = request.DefaultTaxRate,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ExpenseTypes.Add(expenseType);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(expenseType.Id);
    }
}
