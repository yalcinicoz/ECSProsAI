using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberMarketingConsents;

/// <summary>E2: duyuru tercihleri (e-posta/SMS/telefon kampanya izinleri) —
/// Member.Consents jsonb'sinin "marketing" anahtarına güncelleme zamanıyla yazılır
/// (D3'ün acceptedContracts anahtarıyla aynı sözlükte yaşar, ona dokunmaz).</summary>
public record UpdateMemberMarketingConsentsCommand(
    Guid MemberId, bool Email, bool Sms, bool Phone) : IRequest<Result<bool>>;

public class UpdateMemberMarketingConsentsCommandHandler(ICrmDbContext db)
    : IRequestHandler<UpdateMemberMarketingConsentsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateMemberMarketingConsentsCommand request, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (member is null) return Result.Failure<bool>("Üye bulunamadı.");

        var consents = member.Consents is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(member.Consents); // yeni instance — EF jsonb değişikliği görsün
        consents["marketing"] = new Dictionary<string, object>
        {
            ["email"] = request.Email,
            ["sms"] = request.Sms,
            ["phone"] = request.Phone,
            ["updatedAt"] = DateTime.UtcNow
        };
        member.Consents = consents;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
