namespace ECSPros.Api.Services.LegacyStock;

/// <summary>
/// Production MySQL -> PostgreSQL geçici stok snapshot senkronu.
/// Canlı geçişten sonra kapatılır; kalıcı stok otoritesi admin panelidir.
/// </summary>
public sealed class LegacyStockSyncOptions
{
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
    public int StartupDelaySeconds { get; set; } = 90;
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int StockStorageType { get; set; } = 1;
    public int MinimumSourceRows { get; set; } = 1000;
    public bool BlockOnUnmappedQuantity { get; set; } = true;
    public int MaximumUnmappedRows { get; set; }
    public long MaximumUnmappedQuantity { get; set; }
    /// <summary>
    /// Dump sonrasında MySQL'de oluşmuş, stok taşıyan varyant ve raf eşlemelerini hedefte
    /// tamamlayan geçici/on-demand onarım. Normal çalışmada kapalı tutulur.
    /// </summary>
    public bool RepairMissingMappings { get; set; }
    public bool MappingRepairDryRun { get; set; } = true;

    public void Validate()
    {
        if (IntervalSeconds is < 60 or > 86400)
            throw new InvalidOperationException("LegacyStockSync:IntervalSeconds 60-86400 aralığında olmalı.");
        if (StartupDelaySeconds is < 0 or > 3600)
            throw new InvalidOperationException("LegacyStockSync:StartupDelaySeconds 0-3600 aralığında olmalı.");
        if (CommandTimeoutSeconds is < 5 or > 1800)
            throw new InvalidOperationException("LegacyStockSync:CommandTimeoutSeconds 5-1800 aralığında olmalı.");
        if (StockStorageType <= 0)
            throw new InvalidOperationException("LegacyStockSync:StockStorageType pozitif olmalı.");
        if (MinimumSourceRows < 1)
            throw new InvalidOperationException("LegacyStockSync:MinimumSourceRows en az 1 olmalı.");
        if (MaximumUnmappedRows < 0)
            throw new InvalidOperationException("LegacyStockSync:MaximumUnmappedRows negatif olamaz.");
        if (MaximumUnmappedQuantity < 0)
            throw new InvalidOperationException("LegacyStockSync:MaximumUnmappedQuantity negatif olamaz.");
    }
}
