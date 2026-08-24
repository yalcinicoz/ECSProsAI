using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Procurement.Application.Commands.CreateMissingCardNotice;

/// <summary>K9: kart eksik bildirimi — ayrıştırma personeli kart AÇMAZ, katalog sorumlusuna kuyruğa düşer.</summary>
public record CreateMissingCardNoticeCommand(Guid? ReceiptBatchId, string DescriptionText, Guid? CreatedBy)
    : IRequest<Result<Guid>>;

public class CreateMissingCardNoticeCommandHandler(IProcurementDbContext db)
    : IRequestHandler<CreateMissingCardNoticeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateMissingCardNoticeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DescriptionText))
            return Result.Failure<Guid>("Açıklama boş olamaz (aranan ürünü tarif edin).");
        var n = new MissingCardNotice
        {
            ReceiptBatchId = request.ReceiptBatchId,
            DescriptionText = request.DescriptionText.Trim(),
            CreatedBy = request.CreatedBy,
        };
        db.MissingCardNotices.Add(n);
        await db.SaveChangesAsync(ct);
        return Result.Success(n.Id);
    }
}
