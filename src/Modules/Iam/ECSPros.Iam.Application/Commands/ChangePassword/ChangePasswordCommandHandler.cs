using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IIamDbContext _context;
    private readonly IPasswordHasher _hasher;

    public ChangePasswordCommandHandler(IIamDbContext context, IPasswordHasher hasher)
    {
        _context = context;
        _hasher = hasher;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return Result.Failure("Kullanıcı bulunamadı.");

        if (!request.IsAdminReset)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword))
                return Result.Failure("Mevcut şifre gereklidir.");

            if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
                return Result.Failure("Mevcut şifre hatalı.");
        }

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
