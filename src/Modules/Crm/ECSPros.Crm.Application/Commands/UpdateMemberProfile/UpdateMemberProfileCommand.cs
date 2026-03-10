using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberProfile;

public record UpdateMemberProfileCommand(
    Guid MemberId,
    string FirstName,
    string LastName,
    string? Phone,
    string? Gender,
    DateOnly? BirthDate) : IRequest<Result<bool>>;

public class UpdateMemberProfileCommandHandler(ICrmDbContext db)
    : IRequestHandler<UpdateMemberProfileCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateMemberProfileCommand request, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (member is null) return Result.Failure<bool>("Üye bulunamadı.");

        member.FirstName = request.FirstName;
        member.LastName = request.LastName;
        member.Phone = request.Phone;
        member.Gender = request.Gender;
        member.BirthDate = request.BirthDate;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
