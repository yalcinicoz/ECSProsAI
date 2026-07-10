using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.CreateContactMessage;

/// <summary>F3: iletişim formu kaydı — misafir de gönderebilir. Spam frenleri:
/// alan uzunluk sınırları + aynı e-postadan saatte en çok 5 mesaj.</summary>
public record CreateContactMessageCommand(
    Guid FirmPlatformId,
    string Name,
    string Email,
    string Message,
    string? Phone = null,
    string? Subject = null,
    Guid? MemberId = null) : IRequest<Result<Guid>>;

public class CreateContactMessageCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateContactMessageCommand, Result<Guid>>
{
    private const int SaatlikSinir = 5;

    public async Task<Result<Guid>> Handle(CreateContactMessageCommand request, CancellationToken ct)
    {
        var ad = (request.Name ?? "").Trim();
        var eposta = (request.Email ?? "").Trim();
        var mesaj = (request.Message ?? "").Trim();

        if (ad.Length < 2 || ad.Length > 100)
            return Result.Failure<Guid>("Adınızı yazın.");
        if (eposta.Length < 6 || eposta.Length > 200 || !eposta.Contains('@') || !eposta.Contains('.'))
            return Result.Failure<Guid>("Geçerli bir e-posta adresi yazın.");
        if (mesaj.Length < 10)
            return Result.Failure<Guid>("Mesajınız en az 10 karakter olmalıdır.");
        if (mesaj.Length > 4000)
            return Result.Failure<Guid>("Mesajınız en fazla 4000 karakter olabilir.");

        var esik = DateTime.UtcNow.AddHours(-1);
        var sonSayi = await db.ContactMessages
            .CountAsync(m => m.Email == eposta && m.CreatedAt > esik, ct);
        if (sonSayi >= SaatlikSinir)
            return Result.Failure<Guid>("Çok fazla mesaj gönderildi. Lütfen daha sonra tekrar deneyin.");

        var kayit = new ContactMessage
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            Name = ad,
            Email = eposta,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Message = mesaj
        };
        db.ContactMessages.Add(kayit);
        await db.SaveChangesAsync(ct);
        return Result.Success(kayit.Id);
    }
}
