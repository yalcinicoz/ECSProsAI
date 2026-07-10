using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberSessions;

/// <summary>E2: Üyelik Bilgilerim "Aktif Cihazlar" + "Giriş Geçmişi" — üyenin son
/// oturumları (UserAgent/IP login anında kaydedilir; eski kayıtlarda boş olabilir).</summary>
public record GetMemberSessionsQuery(Guid MemberId, int Limit = 10)
    : IRequest<Result<List<MemberSessionDto>>>;

public record MemberSessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsActive,
    string? IpAddress,
    string? UserAgent);

public class GetMemberSessionsQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetMemberSessionsQuery, Result<List<MemberSessionDto>>>
{
    public async Task<Result<List<MemberSessionDto>>> Handle(GetMemberSessionsQuery request, CancellationToken ct)
    {
        var oturumlar = await db.MemberSessions
            .Where(s => s.MemberId == request.MemberId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(s => new MemberSessionDto(
                s.Id, s.CreatedAt, s.ExpiresAt, s.IsActive && s.ExpiresAt > DateTime.UtcNow,
                s.IpAddress, s.UserAgent))
            .ToListAsync(ct);

        return Result.Success(oturumlar);
    }
}
