using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.MarkSortingEntryLabeled;

/// <summary>Etiket basımı işareti (basım tarayıcıda /yazdir/etiket ile yapılır; burada yalnız kayıt tutulur).</summary>
public record MarkSortingEntryLabeledCommand(Guid Id, int Count) : IRequest<Result<bool>>;

public class MarkSortingEntryLabeledCommandHandler(IProcurementDbContext db)
    : IRequestHandler<MarkSortingEntryLabeledCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(MarkSortingEntryLabeledCommand request, CancellationToken ct)
    {
        if (request.Count <= 0) return Result.Failure<bool>("Etiket adedi 0'dan büyük olmalı.");
        var e = await db.SortingEntries.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (e is null) return Result.Failure<bool>("Kayıt bulunamadı.");
        e.LabelPrinted = true;
        e.LabelCount += request.Count;
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
