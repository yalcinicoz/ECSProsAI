using System.Security.Cryptography;
using System.Text;
using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IIamDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(IIamDbContext context, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));

        var session = await _context.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && s.IsActive && !s.IsDeleted, cancellationToken);

        if (session is null || session.ExpiresAt < DateTime.UtcNow)
            return Result.Failure<LoginResponse>("Geçersiz veya süresi dolmuş refresh token.");

        if (!session.User.IsActive || session.User.IsDeleted)
            return Result.Failure<LoginResponse>("Kullanıcı aktif değil.");

        // İzinleri yükle
        var permissions = await _context.UserRoles
            .Where(ur => ur.UserId == session.UserId && !ur.IsDeleted)
            .SelectMany(ur => _context.RolePermissions
                .Where(rp => rp.RoleId == ur.RoleId && !rp.IsDeleted)
                .Select(rp => rp.PermissionId))
            .Union(_context.UserPermissions
                .Where(up => up.UserId == session.UserId && up.GrantType == "grant" && !up.IsDeleted)
                .Select(up => up.PermissionId))
            .Distinct()
            .Join(_context.Permissions.Where(p => p.IsActive && !p.IsDeleted),
                id => id, p => p.Id, (id, p) => p.Code)
            .ToListAsync(cancellationToken);

        var newAccessToken = _jwtTokenService.GenerateAccessToken(session.User, permissions);
        var newRefreshTokenRaw = _jwtTokenService.GenerateRefreshToken();
        var newTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newRefreshTokenRaw)));

        // Eski session'ı kapat, yeni session aç
        session.IsActive = false;

        var newSession = new UserSession
        {
            UserId = session.UserId,
            TokenHash = newTokenHash,
            IpAddress = session.IpAddress,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastActivityAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserSessions.Add(newSession);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponse(
            newAccessToken,
            newRefreshTokenRaw,
            DateTime.UtcNow.AddMinutes(60),
            session.UserId,
            $"{session.User.FirstName} {session.User.LastName}",
            session.User.Email));
    }
}
