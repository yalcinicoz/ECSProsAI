using System.Security.Cryptography;
using System.Text;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IIamDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(IIamDbContext context, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => (u.Username == request.Username || u.Email == request.Username) && !u.IsDeleted, cancellationToken);

        if (user is null || !user.IsActive)
            return Result.Failure<LoginResponse>("Kullanıcı adı veya şifre hatalı.");

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>("Kullanıcı adı veya şifre hatalı.");

        var permissions = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id && !ur.IsDeleted)
            .SelectMany(ur => _context.RolePermissions
                .Where(rp => rp.RoleId == ur.RoleId && !rp.IsDeleted)
                .Select(rp => rp.PermissionId))
            .Union(_context.UserPermissions
                .Where(up => up.UserId == user.Id && up.GrantType == "grant" && !up.IsDeleted)
                .Select(up => up.PermissionId))
            .Distinct()
            .Join(_context.Permissions.Where(p => p.IsActive && !p.IsDeleted),
                id => id, p => p.Id, (id, p) => p.Code)
            .ToListAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions);
        var refreshTokenRaw = _jwtTokenService.GenerateRefreshToken();

        // Refresh token'ı SHA256 ile hash'le (BCrypt gerek yok)
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenRaw)));

        var session = new UserSession
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            IpAddress = "0.0.0.0",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastActivityAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserSessions.Add(session);

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(
            accessToken,
            refreshTokenRaw,
            DateTime.UtcNow.AddMinutes(60),
            user.Id,
            $"{user.FirstName} {user.LastName}",
            user.Email));
    }
}
