using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMember;

public class UpdateMemberCommandHandler : IRequestHandler<UpdateMemberCommand, Result>
{
    private readonly ICrmDbContext _context;

    public UpdateMemberCommandHandler(ICrmDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, cancellationToken);

        if (member is null)
            return Result.Failure("Üye bulunamadı.");

        if (request.MemberGroupId.HasValue)
        {
            var groupExists = await _context.MemberGroups.AnyAsync(g => g.Id == request.MemberGroupId.Value, cancellationToken);
            if (!groupExists)
                return Result.Failure("Üye grubu bulunamadı.");

            member.MemberGroupId = request.MemberGroupId.Value;
        }

        member.FirstName = request.FirstName;
        member.LastName = request.LastName;
        member.Email = request.Email;
        member.Phone = request.Phone;
        member.Gender = request.Gender;
        member.BirthDate = request.BirthDate;
        member.TaxOffice = request.TaxOffice;
        member.TaxNumber = request.TaxNumber;
        member.CompanyName = request.CompanyName;
        member.IsActive = request.IsActive;
        member.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
