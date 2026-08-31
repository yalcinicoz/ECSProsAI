using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Reference;

/// <summary>
/// Pazaryeri referans veritabanı (marketplace_ref) erişim katmanı.
/// Ayrı DB: yeniden indirilebilir cache'tir — ana yedeğe girmez, bozulursa
/// drop + yeniden senkron meşrudur (docs/pazaryeri-entegrasyon-veri-yonetimi.md K1).
/// Ana DB ile FK/join yoktur; köprü her zaman (marketplace, external_id) çiftidir.
/// Bağlantı dizesi yoksa veya DB erişilemezse uygulama etkilenmez; referans senkron
/// uçları anlaşılır hata döner (Redis kalıbıyla aynı hata-güvenlik yaklaşımı).
/// </summary>
public sealed class MarketplaceRefDb : IAsyncDisposable
{
    private readonly string? _connectionString;
    private readonly ILogger<MarketplaceRefDb> _logger;
    private NpgsqlDataSource? _dataSource;
    private bool _schemaReady;

    public MarketplaceRefDb(IConfiguration configuration, ILogger<MarketplaceRefDb> logger)
    {
        _connectionString = configuration.GetConnectionString("MarketplaceRef");
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    /// <summary>
    /// DB'yi (yoksa oluşturarak) ve şemayı hazırlar; başarılıysa data source döner.
    /// Yapılandırılmamışsa/erişilemiyorsa null döner — çağıran anlaşılır hata üretir.
    /// </summary>
    public async Task<NpgsqlDataSource?> GetAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (_dataSource is not null && _schemaReady) return _dataSource;

        try
        {
            _dataSource ??= NpgsqlDataSource.Create(_connectionString!);
            await EnsureSchemaAsync(_dataSource, ct);
            _schemaReady = true;
            return _dataSource;
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000") // database does not exist
        {
            try
            {
                await CreateDatabaseAsync(ct);
                await EnsureSchemaAsync(_dataSource!, ct);
                _schemaReady = true;
                return _dataSource;
            }
            catch (Exception inner)
            {
                _logger.LogWarning(inner, "marketplace_ref veritabanı oluşturulamadı.");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "marketplace_ref veritabanına erişilemiyor.");
            return null;
        }
    }

    private async Task CreateDatabaseAsync(CancellationToken ct)
    {
        var csb = new NpgsqlConnectionStringBuilder(_connectionString!);
        var dbName = csb.Database ?? "marketplace_ref";
        csb.Database = "postgres";
        await using var admin = new NpgsqlConnection(csb.ConnectionString);
        await admin.OpenAsync(ct);
        // CREATE DATABASE parametre almaz; ad connection string'den gelir (config, kullanıcı girdisi değil).
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName.Replace("\"", "")}\"", admin);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("marketplace_ref veritabanı oluşturuldu: {Db}", dbName);
    }

    private static async Task EnsureSchemaAsync(NpgsqlDataSource ds, CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS mp_categories (
                marketplace          text NOT NULL,
                external_id          text NOT NULL,
                parent_external_id   text NULL,
                name                 text NOT NULL,
                path                 text NOT NULL,
                is_leaf              boolean NOT NULL,
                is_active            boolean NOT NULL DEFAULT true,
                first_seen_at        timestamptz NOT NULL DEFAULT now(),
                removed_at           timestamptz NULL,
                raw                  jsonb NULL,
                content_hash         text NOT NULL,
                PRIMARY KEY (marketplace, external_id)
            );
            CREATE INDEX IF NOT EXISTS ix_mp_categories_parent
                ON mp_categories (marketplace, parent_external_id);

            CREATE TABLE IF NOT EXISTS mp_category_attributes (
                marketplace           text NOT NULL,
                category_external_id  text NOT NULL,
                attribute_external_id text NOT NULL,
                code                  text NULL,
                name                  text NOT NULL,
                is_required           boolean NOT NULL,
                allow_custom          boolean NOT NULL,
                is_multi_value        boolean NOT NULL DEFAULT false,
                is_variant_axis       boolean NOT NULL DEFAULT false,
                value_mode            text NOT NULL DEFAULT 'id',
                is_active             boolean NOT NULL DEFAULT true,
                first_seen_at         timestamptz NOT NULL DEFAULT now(),
                removed_at            timestamptz NULL,
                raw                   jsonb NULL,
                content_hash          text NOT NULL,
                PRIMARY KEY (marketplace, category_external_id, attribute_external_id)
            );

            CREATE TABLE IF NOT EXISTS mp_attribute_values (
                marketplace           text NOT NULL,
                category_external_id  text NOT NULL,
                attribute_external_id text NOT NULL,
                value_key             text NOT NULL,
                value_external_id     text NULL,
                value_code            text NULL,
                value                 text NOT NULL,
                is_active             boolean NOT NULL DEFAULT true,
                first_seen_at         timestamptz NOT NULL DEFAULT now(),
                removed_at            timestamptz NULL,
                content_hash          text NOT NULL,
                PRIMARY KEY (marketplace, category_external_id, attribute_external_id, value_key)
            );

            CREATE TABLE IF NOT EXISTS mp_sync_runs (
                id                    uuid PRIMARY KEY,
                marketplace           text NOT NULL,
                scope                 text NOT NULL,
                status                text NOT NULL,
                started_at            timestamptz NOT NULL DEFAULT now(),
                finished_at           timestamptz NULL,
                heartbeat_at          timestamptz NOT NULL DEFAULT now(),
                total_categories      int NULL,
                processed_categories  int NOT NULL DEFAULT 0,
                added_count           int NOT NULL DEFAULT 0,
                changed_count         int NOT NULL DEFAULT 0,
                removed_count         int NOT NULL DEFAULT 0,
                unchanged_count       int NOT NULL DEFAULT 0,
                error                 text NULL
            );
            CREATE INDEX IF NOT EXISTS ix_mp_sync_runs_mp
                ON mp_sync_runs (marketplace, started_at DESC);

            CREATE TABLE IF NOT EXISTS mp_change_log (
                id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                sync_run_id   uuid NOT NULL,
                marketplace   text NOT NULL,
                entity_type   text NOT NULL,
                external_key  text NOT NULL,
                change_type   text NOT NULL,
                change_detail jsonb NULL,
                created_at    timestamptz NOT NULL DEFAULT now(),
                processed_at  timestamptz NULL
            );
            CREATE INDEX IF NOT EXISTS ix_mp_change_log_unprocessed
                ON mp_change_log (marketplace, id) WHERE processed_at IS NULL;

            -- RF1 (2026-08-31): kapsam takibi — bu kategorinin özellik+değerleri en son ne zaman
            -- BAŞARIYLA indirildi (0 özellik dönen kategori de damgalanır; NULL = hiç taranmadı).
            ALTER TABLE mp_categories ADD COLUMN IF NOT EXISTS attributes_synced_at timestamptz NULL;
            """;
        await using var cmd = ds.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null) await _dataSource.DisposeAsync();
    }
}
