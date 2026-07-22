using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberStatus;

/// <summary>
/// H10 sade statü bloğu: üyenin grubunun görünen adı. Tam statü modeli (eşik/kazanç/
/// progress — E13) tanımlanınca bu sorgu genişler; şimdilik yalnız grup adı.
/// </summary>
public record GetMemberStatusQuery(Guid MemberId) : IRequest<Result<MemberStatusDto>>;

public record MemberStatusDto(Dictionary<string, string>? GroupNameI18n);

public class GetMemberStatusQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetMemberStatusQuery, Result<MemberStatusDto>>
{
    public async Task<Result<MemberStatusDto>> Handle(GetMemberStatusQuery request, CancellationToken ct)
    {
        var grupAdi = await db.Members
            .AsNoTracking()
            .Where(m => m.Id == request.MemberId)
            .Select(m => db.MemberGroups
                .Where(g => g.Id == m.MemberGroupId)
                .Select(g => g.NameI18n)
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);

        return Result.Success(new MemberStatusDto(grupAdi));
    }
}
