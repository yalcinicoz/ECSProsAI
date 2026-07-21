using System.Net;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.GenerateApiToken;

public class GenerateApiTokenCommandHandler : IRequestHandler<GenerateApiTokenCommand, Result<ApiTokenResponse>>
{
    // Ömür 15 dk; refresh token yok (istemci gerektiğinde yeniden ister — §4).
    private const int ExpiresInSeconds = 900;
    // İstemci var mı/aktif mi/secret mı yanlış — hepsinde AYNI mesaj (enumeration sızıntısı olmasın).
    private const string GenericError = "Geçersiz istemci kimliği veya parolası.";

    private readonly IIamDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public GenerateApiTokenCommandHandler(IIamDbContext context, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<ApiTokenResponse>> Handle(GenerateApiTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            return Result.Failure<ApiTokenResponse>(GenericError);

        var client = await _context.ApiClients
            .FirstOrDefaultAsync(c => c.ClientId == request.ClientId && !c.IsDeleted, cancellationToken);

        if (client is null || !client.IsActive || client.RevokedAt != null)
            return Result.Failure<ApiTokenResponse>(GenericError);

        if (client.ExpiresAt.HasValue && client.ExpiresAt.Value <= DateTime.UtcNow)
            return Result.Failure<ApiTokenResponse>("İstemci hesabının süresi dolmuş.");

        // IP allowlist — boşsa kısıt yok.
        if (client.IpAllowList.Count > 0 && (request.Ip is null || !client.IpAllowList.Contains(request.Ip)))
            return Result.Failure<ApiTokenResponse>("İstek bu istemci için izin verilen IP'den gelmiyor.");

        // internal hesap yalnız loopback'ten (tam kısıt + nginx deny F3; burada temel guard).
        if (client.ClientType == "internal" && !IsLoopback(request.Ip))
            return Result.Failure<ApiTokenResponse>(GenericError);

        if (!_passwordHasher.Verify(request.ClientSecret, client.SecretHash))
            return Result.Failure<ApiTokenResponse>(GenericError);

        var type = await _context.ApiClientTypes
            .FirstOrDefaultAsync(t => t.Code == client.ApiClientTypeCode && t.IsActive && !t.IsDeleted, cancellationToken);
        if (type is null)
            return Result.Failure<ApiTokenResponse>("İstemci tipi tanımlı değil.");

        // Etkin scope = tip taban paketi (+ gönderim bayrağı, Yol B). Hesapta serbest scope yok.
        var scopes = new HashSet<string>(type.BaseScopes);
        if (client.FulfillmentMode == "supplier")
            foreach (var s in ApiScopes.SupplierFulfillment) scopes.Add(s);

        var token = _jwtTokenService.GenerateApiClientToken(client, scopes);

        client.LastUsedAt = DateTime.UtcNow;
        client.LastUsedIp = request.Ip;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ApiTokenResponse(token, ExpiresInSeconds, "Bearer"));
    }

    private static bool IsLoopback(string? ip)
        => ip is not null && IPAddress.TryParse(ip, out var addr) && IPAddress.IsLoopback(addr);
}
