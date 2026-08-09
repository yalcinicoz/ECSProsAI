using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.DeleteCardMessage;

/// <summary>Ürün Kartı F2: kart mesajını siler (soft delete).</summary>
public record DeleteCardMessageCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteCardMessageCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<DeleteCardMessageCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCardMessageCommand request, CancellationToken ct)
    {
        var mesaj = await db.CardMessages.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (mesaj is null) return Result.Failure<bool>("Mesaj bulunamadı.");
        mesaj.IsDeleted = true;
        mesaj.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
