using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.DeleteMannequin;

public record DeleteMannequinCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteMannequinCommandHandler : IRequestHandler<DeleteMannequinCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public DeleteMannequinCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteMannequinCommand request, CancellationToken ct)
    {
        var mannequin = await _db.Mannequins.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (mannequin is null)
            return Result.Failure<bool>("Manken bulunamadı.");

        // Not: ProductAttribute.CustomValue içindeki mankenId referansları FK ile
        // izlenmiyor (bkz. docs/manken-ozelligi-spec.md) — bu yüzden burada bağımlı
        // kayıt kontrolü yapılmıyor; silme her zaman serbesttir.
        mannequin.IsDeleted = true;
        mannequin.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(true);
    }
}
