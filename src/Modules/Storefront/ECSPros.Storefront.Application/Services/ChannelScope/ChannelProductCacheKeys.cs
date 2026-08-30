namespace ECSPros.Storefront.Application.Services.ChannelScoping;

/// <summary>
/// Kanal görünürlük kümesi (deny-set) süreç-içi önbellek anahtarları. IStorefrontChannelProductFlagService
/// 60 sn cache'ler; kanal kararı/kapsam değiştiren komutlar bu anahtarı ICacheBustPublisher.Bust ile
/// siler (FAZ 10 / A9: yerel IMemoryCache + Redis pub/sub ile diğer düğümlere yayın).
/// </summary>
public static class ChannelProductCacheKeys
{
    public static string Excluded(Guid firmPlatformId) => $"chprod:excluded:{firmPlatformId:N}";
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
}
