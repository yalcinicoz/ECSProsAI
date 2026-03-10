using System.Security.Cryptography;
using System.Text;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.RegisterMember;

public record RegisterMemberCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone = null) : IRequest<Result<Guid>>;

public class RegisterMemberCommandHandler(ICrmDbContext db) : IRequestHandler<RegisterMemberCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterMemberCommand request, CancellationToken ct)
    {
        var exists = await db.Members
            .AnyAsync(m => m.Email == request.Email.ToLowerInvariant(), ct);
        if (exists)
            return Result.Failure<Guid>("Bu e-posta adresi zaten kayıtlı.");

        var defaultGroup = await db.MemberGroups
            .FirstOrDefaultAsync(g => g.IsDefault && !g.IsDeleted, ct);
        if (defaultGroup is null)
            return Result.Failure<Guid>("Varsayılan üye grubu bulunamadı.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password))).ToLowerInvariant();

        var member = new Member
        {
            MemberGroupId = defaultGroup.Id,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = hash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            IsRegistered = true,
            IsActive = true
        };

        db.Members.Add(member);
        await db.SaveChangesAsync(ct);
        return Result.Success(member.Id);
    }
}
