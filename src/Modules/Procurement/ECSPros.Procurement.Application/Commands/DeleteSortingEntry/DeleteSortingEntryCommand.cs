using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.DeleteSortingEntry;

public record DeleteSortingEntryCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteSortingEntryCommandHandler(IProcurementDbContext db)
    : IRequestHandler<DeleteSortingEntryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteSortingEntryCommand request, CancellationToken ct)
    {
        var e = await db.SortingEntries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null) return Result.Failure<bool>("Kayıt bulunamadı.");
        if (e.PutawayStatus == "placed") return Result.Failure<bool>("Yerleştirilmiş kayıt silinemez (stok girmiştir).");
        e.IsDeleted = true;
        e.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
