using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IIamDbContext _context;

    public UpdateUserCommandHandler(IIamDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result.Failure("Kullanıcı bulunamadı.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Department = request.Department;
        user.JobTitle = request.JobTitle;
        user.Phone = request.Phone;
        user.IsActive = request.IsActive;
        user.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
