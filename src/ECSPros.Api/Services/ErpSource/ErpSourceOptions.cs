namespace ECSPros.Api.Services.ErpSource;

/// <summary>
/// V3 ERP/SQL Server -> ECSPros PostgreSQL kalıcı kaynak senkronu ayarları.
/// Güvenli varsayılan: servis kapalı ve dry-run. Bağlantı dizesi repoya yazılmaz;
/// environment/secret üzerinden ErpSource__ConnectionString olarak verilir.
/// </summary>
public sealed class ErpSourceOptions
{
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public bool CatalogEnabled { get; set; } = true;
    public bool PriceEnabled { get; set; } = true;
    public string ConnectionString { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int StartupDelaySeconds { get; set; } = 90;
    public int CatalogMinutes { get; set; } = 15;
    public int PriceMinutes { get; set; } = 10;
    public int OverlapMinutes { get; set; } = 30;
    public bool ProductAttributeReconciliationEnabled { get; set; } = true;
    public int ProductAttributeBatchSize { get; set; } = 100;
    public DateTime InitialSinceUtc { get; set; } = new(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);
    public string SourceTimeZoneId { get; set; } = "Europe/Istanbul";

    public string CatalogProcedure { get; set; } = "jld_Appurunler";
    public string VariantProcedure { get; set; } = "jld_AppurunVaryantlari";
    public bool TargetedRefreshEnabled { get; set; } = true;
    public bool AutoCreateColorValues { get; set; } = true;
    public bool AutoCreateProductAttributeValues { get; set; } = true;

    /// <summary>V3 tedarikçi AttributeCode -> ECSPros accounts.current_accounts.Code.</summary>
    public Dictionary<string, string> SupplierAccountCodes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ERP urunGrubu değeri -> definition.product_groups.Code. Eşleşmeyen yeni ürün yazılmaz.</summary>
    public Dictionary<string, string> ProductGroupCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kot Ceket"] = "grp_46",
        ["Eşofman Altı"] = "grp_47",
        ["Sütyen"] = "grp_118",
        ["Triko Hırka"] = "grp_14"
    };

    /// <summary>V3 varyantTipId -> definition.attribute_types.Code.</summary>
    public Dictionary<int, string> VariantAttributeTypeCodes { get; set; } = new()
    {
        [1] = "renk",
        [2] = "beden"
    };

    /// <summary>V3 prItemAttribute.AttributeTypeCode -> definition.attribute_types.Code.</summary>
    public Dictionary<string, string> ProductAttributeTypeCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = "season",
        ["6"] = "marka",
        ["10"] = "cinsiyet",
        ["17"] = "malzeme",
        ["18"] = "urun_boyu",
        ["19"] = "ic_uzunluk",
        ["20"] = "kalip",
        ["21"] = "astar_durumu",
        ["22"] = "fermuar",
        ["23"] = "esneklik",
        ["24"] = "topuk_boyu",
        ["25"] = "dis_materyal",
        ["28"] = "taban_yuksekligi",
        ["29"] = "taban_ozelligi",
        ["31"] = "ic_cep",
        ["32"] = "aski_tipi",
        ["33"] = "aski_boyu",
        ["35"] = "canta_agzi",
        ["36"] = "dolgu",
        ["37"] = "balen",
        ["44"] = "boy",
        ["45"] = "desen",
        ["51"] = "yas_grubu",
        ["53"] = "topuk_boyu",
        ["54"] = "topuk_tipi",
        ["55"] = "ortam",
        ["56"] = "bel",
        ["57"] = "kumas_turu",
        ["71"] = "malzeme"
    };

    /// <summary>
    /// ERP'de operasyonel/merchandising metadata olup katalog attribute'u olarak taşınmayacağı
    /// açıkça onaylanan keywordId'ler. Listede olmayan eşlenmemiş yeni kod fail-closed davranır.
    /// </summary>
    public List<string> IgnoredProductAttributeTypeCodes { get; set; } =
        ["2", "3", "4", "5", "7", "8", "9", "11", "12", "13", "14", "30", "34", "50", "58"];

    /// <summary>Hedef type code -> ERP değer adı -> hedef definition değer adı.</summary>
    public Dictionary<string, Dictionary<string, string>> ProductAttributeValueAliases { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["marka"] = new(StringComparer.OrdinalIgnoreCase) { ["julude.com"] = "julude" }
        };

    /// <summary>Firma platform kodu -> ERP sonuç kolonları.</summary>
    public Dictionary<string, ErpChannelPriceOptions> ChannelPrices { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mishar"] = new()
        {
            PriceColumn = "tozluSatisFiyati",
            CompareAtPriceColumn = "tozluListeFiyati"
        }
    };

    public void Validate()
    {
        var ignored = IgnoredProductAttributeTypeCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = ProductAttributeTypeCodes.Keys.Where(ignored.Contains).ToArray();
        if (overlap.Length > 0)
            throw new InvalidOperationException(
                $"ErpSource product attribute kodları hem mapped hem ignored olamaz: {string.Join(", ", overlap)}.");
        if (ProductAttributeTypeCodes.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("ErpSource product attribute mapping anahtar/değerleri boş olamaz.");
        var mappedTargetCodes = ProductAttributeTypeCodes.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphanAliases = ProductAttributeValueAliases.Keys.Where(x => !mappedTargetCodes.Contains(x)).ToArray();
        if (orphanAliases.Length > 0)
            throw new InvalidOperationException(
                $"ErpSource value alias hedefi mapped attribute type değil: {string.Join(", ", orphanAliases)}.");
        if (StartupDelaySeconds is < 0 or > 600)
            throw new InvalidOperationException("ErpSource:StartupDelaySeconds 0-600 aralığında olmalı.");
        if (SupplierAccountCodes.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("ErpSource supplier account mapping anahtar/değerleri boş olamaz.");
        if (ProductGroupCodes.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("ErpSource product group mapping anahtar/değerleri boş olamaz.");

        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ErpSource etkin fakat ConnectionString boş.");
        if (CatalogEnabled && (string.IsNullOrWhiteSpace(CatalogProcedure) || string.IsNullOrWhiteSpace(VariantProcedure)))
            throw new InvalidOperationException("ERP katalog prosedürleri yapılandırılmamış.");
        if (CommandTimeoutSeconds is < 5 or > 3600)
            throw new InvalidOperationException("ErpSource:CommandTimeoutSeconds 5-3600 aralığında olmalı.");
        if (ProductAttributeBatchSize is < 1 or > 500)
            throw new InvalidOperationException("ErpSource:ProductAttributeBatchSize 1-500 aralığında olmalı.");
    }
}

public sealed class ErpChannelPriceOptions
{
    public string PriceColumn { get; set; } = "";
    public string CompareAtPriceColumn { get; set; } = "";
}
