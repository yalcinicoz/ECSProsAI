using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.UpdateSortingEntry;

public record UpdateSortingEntryCommand(Guid Id, decimal Quantity, decimal? UnitCost) : IRequest<Result<bool>>;

public class UpdateSortingEntryCommandHandler(IProcurementDbContext db)
    : IRequestHandler<UpdateSortingEntryCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateSortingEntryCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<bool>("Adet 0'dan büyük olmalı.");
        if (request.UnitCost is < 0) return Result.Failure<bool>("Maliyet negatif olamaz.");
        var e = await db.SortingEntries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null) return Result.Failure<bool>("Kayıt bulunamadı.");
        if (e.PutawayStatus == "placed") return Result.Failure<bool>("Yerleştirilmiş kayıt düzenlenemez (stok girmiştir).");
        e.Quantity = request.Quantity;
        e.UnitCost = request.UnitCost;
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
