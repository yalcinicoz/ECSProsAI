using ECSPros.Iam.Application.Commands.LoginSupplierUser;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.RefreshSupplierUserToken;

public record RefreshSupplierUserTokenCommand(string RefreshToken) : IRequest<Result<SupplierUserLoginResponse>>;

public class RefreshSupplierUserTokenCommandHandler(IIamDbContext db, ISupplierUserTokenService tokenService)
    : IRequestHandler<RefreshSupplierUserTokenCommand, Result<SupplierUserLoginResponse>>
{
    public async Task<Result<SupplierUserLoginResponse>> Handle(RefreshSupplierUserTokenCommand request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var session = await db.SupplierUserSessions
            .Include(s => s.SupplierUser)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.IsActive && s.ExpiresAt > DateTime.UtcNow, ct);

        if (session is null || !session.SupplierUser.IsActive)
            return Result.Failure<SupplierUserLoginResponse>("Geçersiz veya süresi dolmuş refresh token.");

        // Rotate
        session.IsActive = false;
        session.UpdatedAt = DateTime.UtcNow;

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var expiresAt = DateTime.UtcNow.AddDays(30);

        db.SupplierUserSessions.Add(new SupplierUserSession
        {
            SupplierUserId = session.SupplierUserId,
            RefreshTokenHash = refreshHash,
            ExpiresAt = expiresAt,
            IsActive = true,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent
        });

        await db.SaveChangesAsync(ct);

        var user = session.SupplierUser;
        var accessToken = tokenService.GenerateAccessToken(user);
        return Result.Success(new SupplierUserLoginResponse(
            accessToken, rawRefresh, expiresAt,
            user.Id, user.CurrentAccountId, user.FullName, user.Email));
    }
}
