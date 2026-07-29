using System.Security.Cryptography;
using System.Text;
using ECSPros.Crm.Application.Services;

namespace ECSPros.Crm.Infrastructure.Services;

/// <summary>D5: BCrypt (workFactor 12, IAM'la aynı). Eski SHA256 hash'leri yalnız
/// doğrulamada tanınır — yeni yazımlar her zaman BCrypt; başarılı girişte
/// LoginMemberCommandHandler NeedsRehash ile eski hash'i BCrypt'e yükseltir.</summary>
public class MemberPasswordHasher : IMemberPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        if (storedHash.StartsWith("$2"))
            return BCrypt.Net.BCrypt.Verify(password, storedHash);

        // Legacy MD5 — juludedb webmembers.password (32 hane hex, büyük harf). Aktarılan
        // Mishar üyeleri (2026-07-23): eski şifreyle giriş → başarıda BCrypt'e yükseltilir
        // (NeedsRehash $2 dışını true döner). Düz MD5(UTF8(password)) varsayımı.
        if (storedHash.Length == 32)
        {
            var md5 = MD5.HashData(Encoding.UTF8.GetBytes(password));
            return string.Equals(storedHash, Convert.ToHexString(md5), StringComparison.OrdinalIgnoreCase);
        }

        // Legacy SHA256 — hex (eski register/login) veya Base64 (eski admin CreateMember)
        var sha = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        if (storedHash.Length == 64)
            return string.Equals(storedHash, Convert.ToHexString(sha), StringComparison.OrdinalIgnoreCase);
        if (storedHash.Length == 44)
            return storedHash == Convert.ToBase64String(sha);
        return false;
    }

    public bool NeedsRehash(string storedHash) =>
        !string.IsNullOrEmpty(storedHash) && !storedHash.StartsWith("$2");
}
