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
            member.CreatedAt));
    }
}
