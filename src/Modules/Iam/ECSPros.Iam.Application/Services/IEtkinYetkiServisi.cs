using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Iam.Application.Services;

/// <summary>
/// Kullanıcının EFEKTİF yetkilerinin tek kaynağı (Y1, karar K3 "anında etkili").
///
/// Yetki artık JWT'ye GÖMÜLMEZ: token yalnız kimlik + süper admin bayrağı taşır, kontrol her
/// istekte buradan okunur. Böylece verilen yetki hemen kullanılabilir, kaldırılan yetki hemen
/// kapanır — token ömrü (60 dk) beklenmez.
///
/// Performans (tasarım §C.4): L1 süreç-içi bellek (kısa) + L2 dağıtık cache; yetki değişince
/// <see cref="GecersizKilAsync"/> ile düşürülür. Cache erişilemezse DB'den okunur —
/// <b>fail-safe kapalıdır</b>: cache yokluğu yetkiyi asla AÇIK varsaymaz.
/// </summary>
public interface IEtkinYetkiServisi
{
    Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tek kullanıcının önbelleğini düşür (yetki/grup/istisna/süper admin değişimi).</summary>
    Task GecersizKilAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Bir yetki grubunun (rol) tüm üyelerinin önbelleğini düşür — grup içeriği değişince.</summary>
    Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default);
}
