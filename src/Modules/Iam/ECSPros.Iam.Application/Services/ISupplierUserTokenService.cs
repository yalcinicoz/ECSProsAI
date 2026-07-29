using ECSPros.Iam.Domain.Entities;

namespace ECSPros.Iam.Application.Services;

/// <summary>SupplierUser (pazaryeri satıcısı panel kullanıcısı) için parola akışı token servisi —
/// type=supplier_user + owner_id (cari kart). MemberTokenService kalıbı (60 dk access + refresh).</summary>
public interface ISupplierUserTokenService
{
    string GenerateAccessToken(SupplierUser user);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
