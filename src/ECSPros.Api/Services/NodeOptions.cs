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
///   Node:WorkerProfile    → All | LegacyImport | LegacyStock | ErpSource — Worker/Both düğümde hangi worker
///                           grubunun kaydedileceğini seçer (varsayılan: All)
///   Node:MigrateOnStartup → false ise açılışta EF migration + seed ÇALIŞMAZ
///                           (A7: çoklu düğümde migration yarışı önlenir; migration
///                           deploy adımında tek düğümden `dotnet ef database update`)
/// </summary>
public sealed class NodeOptions
{
    private static readonly string[] GecerliRoller = ["Api", "Worker", "Both"];
    private static readonly string[] GecerliWorkerProfilleri = ["All", "LegacyImport", "LegacyStock", "ErpSource"];

    public string Id { get; set; } = Environment.MachineName;
    public string Role { get; set; } = "Both";
    public string WorkerProfile { get; set; } = "All";
    public bool MigrateOnStartup { get; set; } = true;

    public void Dogrula()
    {
        Id = Id?.Trim() ?? string.Empty;
        if (Id.Length == 0)
            throw new InvalidOperationException("Node:Id boş olamaz.");

        var canonicalRole = GecerliRoller.FirstOrDefault(x =>
            string.Equals(x, Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonicalRole is null)
            throw new InvalidOperationException(
                $"Geçersiz Node:Role '{Role}'. İzin verilen değerler: {string.Join(", ", GecerliRoller)}.");

        Role = canonicalRole;

        var canonicalWorkerProfile = GecerliWorkerProfilleri.FirstOrDefault(x =>
            string.Equals(x, WorkerProfile?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (canonicalWorkerProfile is null)
            throw new InvalidOperationException(
                $"Geçersiz Node:WorkerProfile '{WorkerProfile}'. İzin verilen değerler: " +
                $"{string.Join(", ", GecerliWorkerProfilleri)}.");

        WorkerProfile = canonicalWorkerProfile;
    }

    /// <summary>Arka plan worker'ları bu düğümde başlamalı mı?</summary>
    public bool WorkerRolu => Role is "Worker" or "Both";

    /// <summary>Standart uygulama worker grubunun tamamı bu düğümde başlamalı mı?</summary>
    public bool GenelWorkerRolu => WorkerRolu && WorkerProfile == "All";

    /// <summary>Geçici production MySQL import worker'ı bu düğümde başlamalı mı?</summary>
    public bool LegacyImportWorkerRolu => WorkerRolu && WorkerProfile is "All" or "LegacyImport";

    /// <summary>Geçiş süresince production MySQL'den stok snapshot'ı alan izole worker mı?</summary>
    public bool LegacyStockWorkerRolu => WorkerRolu && WorkerProfile is "All" or "LegacyStock";

    /// <summary>Kalıcı V3 ERP kaynak worker'ı bu düğümde başlamalı mı?</summary>
    public bool ErpSourceWorkerRolu => WorkerRolu && WorkerProfile is "All" or "ErpSource";

    /// <summary>Bu process yalnız geçici legacy import worker'ı için mi ayrıldı?</summary>
    public bool SadeceLegacyImport => WorkerRolu && WorkerProfile == "LegacyImport";

    /// <summary>Bu process yalnız tek bir izole worker profili için mi ayrıldı?</summary>
    public bool SadeceIzoleWorker => WorkerRolu && WorkerProfile is "LegacyImport" or "LegacyStock" or "ErpSource";
}
