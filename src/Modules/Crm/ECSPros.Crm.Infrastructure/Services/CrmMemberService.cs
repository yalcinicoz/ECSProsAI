using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Infrastructure.Services;

/// <summary>
/// IMemberService implementasyonu — diğer modüllerin (G5 vitrin koleksiyon kartı üye adı)
/// üye bilgisine CRM'e doğrudan referans vermeden erişmesi için (CatalogProductService
/// deseni; sözleşme Faz 5'ten beri vardı, ilk tüketici G5).
/// </summary>
public class CrmMemberService(ICrmDbContext db) : IMemberService
{
    public async Task<MemberInfo?> GetMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        return await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new MemberInfo(
                m.Id,
                (m.FirstName + " " + m.LastName).Trim(),
                m.Email, m.Phone, m.MemberGroupId, m.IsActive,
                m.Gender, m.CityId,
                db.Addresses.Where(a => a.MemberId == m.Id && a.IsDefault && !a.IsDeleted)
                    .Select(a => a.CityId).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> MemberExistsAsync(Guid memberId, CancellationToken ct = default) =>
        db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, ct);
}
