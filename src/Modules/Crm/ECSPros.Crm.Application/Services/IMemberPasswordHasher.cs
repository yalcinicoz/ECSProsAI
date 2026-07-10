namespace ECSPros.Crm.Application.Services;

/// <summary>D5: üye şifre hash'leme — BCrypt (IAM ile aynı workFactor).
/// Verify eski SHA256 hash'lerini de tanır (hex — eski register; Base64 — eski admin
/// CreateMember); NeedsRehash true dönerse başarılı girişte BCrypt'e yükseltilir.</summary>
public interface IMemberPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
    bool NeedsRehash(string storedHash);
}
