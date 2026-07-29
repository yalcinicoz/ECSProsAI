using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.LoginSupplierUser;

public record LoginSupplierUserCommand(
    string Email, string Password,
    string? IpAddress = null, string? UserAgent = null)
    : IRequest<Result<SupplierUserLoginResponse>>;

public record SupplierUserLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid SupplierUserId,
    Guid CurrentAccountId,
    string FullName,
    string Email);

public class LoginSupplierUserCommandHandler(
    IIamDbContext db, ISupplierUserTokenService tokenService, IPasswordHasher passwordHasher)
    : IRequestHandler<LoginSupplierUserCommand, Result<SupplierUserLoginResponse>>
{
    public async Task<Result<SupplierUserLoginResponse>> Handle(LoginSupplierUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.SupplierUsers.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        // Generic hata — kullanıcı var mı yok mu sızdırma
        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<SupplierUserLoginResponse>("E-posta veya şifre hatalı.");

        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshHash = tokenService.HashRefreshToken(rawRefresh);
        var expiresAt = DateTime.UtcNow.AddDays(30);

        db.SupplierUserSessions.Add(new SupplierUserSession
        {
            SupplierUserId = user.Id,
            RefreshTokenHash = refreshHash,
            ExpiresAt = expiresAt,
            IsActive = true,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);
        return Result.Success(new SupplierUserLoginResponse(
            accessToken, rawRefresh, expiresAt,
            user.Id, user.CurrentAccountId, user.FullName, user.Email));
    }
}
