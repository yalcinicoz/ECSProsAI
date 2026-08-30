namespace ECSPros.Api.Services;

/// <summary>
/// FAZ 11 / K1 — ASP.NET Core Forwarded Headers güven sınırı. Yalnız burada açıkça
/// yazılan Nginx/LB adresleri forwarding başlıklarını değiştirebilir. Geniş RFC1918
/// ağları güvenli varsayılan değildir; üretim adresleri ortama özel config'te tutulur.
/// </summary>
public sealed class ReverseProxyOptions
{
    public string[] KnownProxies { get; set; } = ["127.0.0.1", "::1"];
    public string[] KnownNetworks { get; set; } = [];
    public int ForwardLimit { get; set; } = 1;

    public Microsoft.AspNetCore.Builder.ForwardedHeadersOptions CreateForwardedHeadersOptions()
    {
        if (ForwardLimit is < 1 or > 5)
            throw new InvalidOperationException("ReverseProxy:ForwardLimit 1 ile 5 arasında olmalıdır.");

        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost,
            ForwardLimit = ForwardLimit
        };
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var value in KnownProxies)
        {
            if (!System.Net.IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException($"Geçersiz ReverseProxy:KnownProxies adresi: {value}");
            options.KnownProxies.Add(address);
        }

        foreach (var value in KnownNetworks)
        {
            var parts = value.Split('/', 2);
            if (parts.Length != 2 || !System.Net.IPAddress.TryParse(parts[0], out var prefix)
                || !int.TryParse(parts[1], out var prefixLength))
                throw new InvalidOperationException($"Geçersiz ReverseProxy:KnownNetworks CIDR değeri: {value}");
            var maxPrefix = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            if (prefixLength < 0 || prefixLength > maxPrefix)
                throw new InvalidOperationException($"Geçersiz ReverseProxy:KnownNetworks prefix değeri: {value}");
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }

        return options;
    }
}
