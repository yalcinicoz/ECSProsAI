using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.AssignRole;

public class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Result>
{
    private readonly IIamDbContext _context;
    private readonly IEtkinYetkiServisi _yetkiServisi;

    public AssignRoleCommandHandler(IIamDbContext context, IEtkinYetkiServisi yetkiServisi)
    {
        _context = context;
        _yetkiServisi = yetkiServisi;
    }

    public async Task<Result> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (!userExists)
            return Result.Failure("Kullanıcı bulunamadı.");

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, cancellationToken);
        if (!roleExists)
            return Result.Failure("Rol bulunamadı.");

        var alreadyAssigned = await _context.UserRoles.AnyAsync(
            ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId && !ur.IsDeleted,
            cancellationToken);

        if (alreadyAssigned)
            return Result.Failure("Bu rol zaten atanmış.");

        _context.UserRoles.Add(new UserRole { UserId = request.UserId, RoleId = request.RoleId });
        await _context.SaveChangesAsync(cancellationToken);
        // K3: yetki değişikliği ANINDA etkili — kullanıcının efektif yetki önbelleği düşürülür.
        await _yetkiServisi.GecersizKilAsync(request.UserId, cancellationToken);
        return Result.Success();
    }
}
