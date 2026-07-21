using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Iam.Application.Commands.GenerateApiToken;

/// <summary>OAuth2 client_credentials — API hesabı clientId+clientSecret ile access token alır.</summary>
public record GenerateApiTokenCommand(string ClientId, string ClientSecret, string? Ip)
    : IRequest<Result<ApiTokenResponse>>;

public record ApiTokenResponse(string AccessToken, int ExpiresIn, string TokenType);
