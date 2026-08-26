using System.Text.Json;

namespace ECSPros.Shared.Kernel.DesignTime;

/// <summary>
/// Faz 0 (sır hijyeni): design-time (dotnet ef) bağlantı dizesi — git'e SIR YAZILMAZ.
/// Sıra: 1) ECSPROS_DB ortam değişkeni, 2) untracked appsettings.Production.json
/// (DefaultConnection), 3) şifresiz localhost (yalnız yerel güven/peer auth ortamları için).
/// </summary>
public static class DesignTimeConnection
{
    public static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("ECSPROS_DB");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        // Repo köküne göre Api'nin untracked prod ayarı (sunucuda migration bu yolla çalışır)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ECSPros.Api", "appsettings.Production.json");
            if (File.Exists(candidate))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                        && cs.TryGetProperty("DefaultConnection", out var def)
                        && def.GetString() is { Length: > 0 } val)
                        return val;
                }
                catch (JsonException) { }
            }
            dir = dir.Parent;
        }
        return "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce";
    }
}
