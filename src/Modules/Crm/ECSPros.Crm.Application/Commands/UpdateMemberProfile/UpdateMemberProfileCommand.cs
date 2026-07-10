using ECSPros.Crm.Application.Commands.SendLoginOtp;
using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberProfile;

/// <summary>E2 genişletmesi: telefon normalize (TR ülke kodlu) + benzersizlik denetimi,
/// telefon değişince doğrulama düşer (yeniden SMS doğrulaması gerekir); CityId (G9
/// kişiselleştirme segmenti). E-posta bu komutla DEĞİŞMEZ — giriş kimliğidir,
/// değişikliği doğrulama akışı ister (ileri faz).</summary>
public record UpdateMemberProfileCommand(
    Guid MemberId,
    string FirstName,
    string LastName,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate,
    Guid? CityId = null) : IRequest<Result<bool>>;

public class UpdateMemberProfileCommandHandler(ICrmDbContext db)
    : IRequestHandler<UpdateMemberProfileCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateMemberProfileCommand request, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (member is null) return Result.Failure<bool>("Üye bulunamadı.");

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Result.Failure<bool>("Ad ve soyad zorunludur.");

        string? telefon = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            telefon = OtpHelper.Normalize(request.Phone);
            if (telefon is null)
                return Result.Failure<bool>("Geçerli bir telefon numarası girin.");

            if (telefon != member.Phone)
            {
                var kullanimda = await db.Members.AnyAsync(
                    m => m.Id != member.Id && m.Phone == telefon, ct);
                if (kullanimda)
                    return Result.Failure<bool>("Bu telefon numarası başka bir üyede kayıtlı.");
                member.IsPhoneVerified = false; // yeni numara yeniden doğrulanmalı (SMS girişi doğrular)
            }
        }

        if (request.CityId is { } cityId)
        {
            var sehirVar = await db.Cities.AnyAsync(c => c.Id == cityId, ct);
            if (!sehirVar) return Result.Failure<bool>("Seçilen şehir bulunamadı.");
        }

        member.FirstName = request.FirstName.Trim();
        member.LastName = request.LastName.Trim();
        member.Phone = telefon;
        member.Gender = request.Gender;
        member.BirthDate = request.BirthDate;
        member.CityId = request.CityId;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
