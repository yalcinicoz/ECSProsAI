using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberMarketingConsents;

/// <summary>E2: duyuru tercihleri (e-posta/SMS/telefon kampanya izinleri) —
/// Member.Consents jsonb'sinin "marketing" anahtarına güncelleme zamanıyla yazılır
/// (D3'ün acceptedContracts anahtarıyla aynı sözlükte yaşar, ona dokunmaz).</summary>
public record UpdateMemberMarketingConsentsCommand(
    Guid MemberId, bool Email, bool Sms, bool Phone, bool? Push = null) : IRequest<Result<bool>>;   // Push: mobil pazarlama push izni (2026-09-07; null → mevcut korunur)

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
        bool eskiPush = false;
        if (consents.TryGetValue("marketing", out var eski))
        {
            if (eski is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                eskiPush = je.TryGetProperty("push", out var pv) && pv.ValueKind == System.Text.Json.JsonValueKind.True;
            else if (eski is Dictionary<string, object> d) eskiPush = d.TryGetValue("push", out var v) && v is true;
        }
        consents["marketing"] = new Dictionary<string, object>
        {
            ["email"] = request.Email,
            ["sms"] = request.Sms,
            ["phone"] = request.Phone,
            ["push"] = request.Push ?? eskiPush,   // KVKK: varsayılan false, kullanıcı açar
            ["updatedAt"] = DateTime.UtcNow
        };
        member.Consents = consents;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
