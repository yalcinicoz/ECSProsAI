using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IIamDbContext _context;
    private readonly IPasswordHasher _hasher;

    public CreateUserCommandHandler(IIamDbContext context, IPasswordHasher hasher)
    {
        _context = context;
        _hasher = hasher;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Users.AnyAsync(
            u => (u.Username == request.Username || u.Email == request.Email) && !u.IsDeleted,
            cancellationToken);

        if (exists)
            return Result.Failure<Guid>("Bu kullanıcı adı veya e-posta zaten kullanımda.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Department = request.Department,
            JobTitle = request.JobTitle,
            Phone = request.Phone,
            IsActive = true,
            MustChangePassword = request.MustChangePassword
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
