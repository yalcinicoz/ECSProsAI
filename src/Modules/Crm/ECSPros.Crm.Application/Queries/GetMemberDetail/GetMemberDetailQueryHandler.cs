using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberDetail;

public class GetMemberDetailQueryHandler : IRequestHandler<GetMemberDetailQuery, Result<MemberDetailDto>>
{
    private readonly ICrmDbContext _context;

    public GetMemberDetailQueryHandler(ICrmDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MemberDetailDto>> Handle(GetMemberDetailQuery request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken);

        if (member is null)
            return Result.Failure<MemberDetailDto>("Üye bulunamadı.");

        // E2: duyuru tercihleri Consents jsonb'nin "marketing" anahtarında
        // (jsonb'den JsonElement, aynı süreçte yazılmışsa DTO gelebilir).
        // "marketing" anahtarı hiç yoksa e-posta/SMS/telefon KAPALI, mobil push AÇIK döner: push izni opt-out'tur
        // (OS bildirim izni cihaz kaydında alınır; üye uygulamadan kapatmadıkça pazarlama push'u gider — 2026-09-08).
        MarketingConsentsDto? pazarlama = new MarketingConsentsDto(false, false, false, true);
        if (member.Consents is not null && member.Consents.TryGetValue("marketing", out var m))
        {
            if (m is MarketingConsentsDto dto) pazarlama = dto;
            else if (m is System.Text.Json.JsonElement je
                     && je.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                bool Acik(string ad) => je.TryGetProperty(ad, out var v)
                    && v.ValueKind == System.Text.Json.JsonValueKind.True;
                bool Kapali(string ad) => je.TryGetProperty(ad, out var v)
                    && v.ValueKind == System.Text.Json.JsonValueKind.False;
                pazarlama = new MarketingConsentsDto(Acik("email"), Acik("sms"), Acik("phone"), !Kapali("push"));
            }
        }

        return Result.Success(new MemberDetailDto(
            member.Id,
            member.MemberGroupId,
            member.FirstName,
            member.LastName,
            member.Email,
            member.Phone,
            member.Gender,
            member.BirthDate,
            member.TaxOffice,
            member.TaxNumber,
            member.CompanyName,
            member.IsRegistered,
            member.IsEmailVerified,
            member.IsPhoneVerified,
            member.IsActive,
            member.LastLoginAt,
            member.CreatedAt,
            IdentityVerified: member.IdentityVerifiedAt != null,
            CityId: member.CityId,
            MarketingConsents: pazarlama));
    }
}
