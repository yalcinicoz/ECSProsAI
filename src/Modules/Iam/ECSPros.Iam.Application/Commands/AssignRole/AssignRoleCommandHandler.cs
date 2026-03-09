using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.AssignRole;

public class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Result>
{
    private readonly IIamDbContext _context;

    public AssignRoleCommandHandler(IIamDbContext context)
    {
        _context = context;
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
        return Result.Success();
    }
}
