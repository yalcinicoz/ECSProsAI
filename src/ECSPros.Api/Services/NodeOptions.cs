namespace ECSPros.Api.Services;

/// <summary>
/// FAZ 10 / A2 — düğüm kimliği ve rolü (çoklu sunucu HA-lite).
/// appsettings "Node" bölümünden okunur; hiç yapılandırılmamışsa tek sunucu davranışı
/// birebir korunur (Role=Both, MigrateOnStartup=true).
///
///   Node:Id               → loglarda/health'te düğüm adı (varsayılan: makine adı)
///   Node:Role             → Api | Worker | Both — arka plan worker'ları yalnız
///                           Worker/Both düğümde başlar (P0-5 geçici koruması;
///                           kalıcı dağıtık claim Kademe B / B2'nin konusu)
///   Node:MigrateOnStartup → false ise açılışta EF migration + seed ÇALIŞMAZ
///                           (A7: çoklu düğümde migration yarışı önlenir; migration
///                           deploy adımında tek düğümden `dotnet ef database update`)
/// </summary>
public sealed class NodeOptions
{
    public string Id { get; set; } = Environment.MachineName;
    public string Role { get; set; } = "Both";
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Arka plan worker'ları bu düğümde başlamalı mı?</summary>
    public bool WorkerRolu => !string.Equals(Role, "Api", StringComparison.OrdinalIgnoreCase);
}
