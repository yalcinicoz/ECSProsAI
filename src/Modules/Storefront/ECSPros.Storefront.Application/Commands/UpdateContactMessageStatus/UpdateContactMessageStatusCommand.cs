using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpdateContactMessageStatus;

/// <summary>P5: gelen kutusu durum akışı — new → read (yanlış işaretlendiyse geri new yapılabilir).</summary>
public record UpdateContactMessageStatusCommand(Guid Id, string Status) : IRequest<Result<bool>>;

public class UpdateContactMessageStatusCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpdateContactMessageStatusCommand, Result<bool>>
{
    private static readonly string[] GecerliDurumlar = ["new", "read"];

    public async Task<Result<bool>> Handle(UpdateContactMessageStatusCommand request, CancellationToken ct)
    {
        if (!GecerliDurumlar.Contains(request.Status))
            return Result.Failure<bool>($"Geçersiz durum: {request.Status} (new | read)");

        var mesaj = await db.ContactMessages.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (mesaj is null)
            return Result.Failure<bool>("İletişim mesajı bulunamadı");

        mesaj.Status = request.Status;
        mesaj.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
