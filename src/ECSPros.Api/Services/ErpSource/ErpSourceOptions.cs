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
    // Activate only after the panel/config dry-run comparison; no automatic data migration.
    public bool UsePanelGroupMappings { get; set; }
    public string MappingTargetSystem { get; set; } = "erp:nebim";
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

    public bool SupplierReconciliationEnabled { get; set; } = true;
    public int SupplierReconciliationMinutes { get; set; } = 60;
    public string SupplierAccountCodePrefix { get; set; } = "V3-SUP-";

    /// <summary>V3 tedarikçi AttributeCode -> ECSPros accounts.current_accounts.Code.</summary>
    public Dictionary<string, string> SupplierAccountCodes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string BuildSupplierAccountCode(string sourceCode)
    {
        var code = $"{SupplierAccountCodePrefix}{sourceCode.Trim()}";
        if (code.Length > 50)
            throw new InvalidOperationException($"V3 tedarikçi cari kodu 50 karakteri aşıyor: {sourceCode}.");
        return code;
    }

    /// <summary>
    /// ERP urunGrubu değeri -> definition.product_groups.Code. Ad birebir eşleşme (Norm) bu sözlükten ÖNCE gelir
    /// (2026-09-06 kararı: katman yok, "Triko Bluz" kendi grubudur). Sözlük yalnız yazımı farklı adlar içindir
    /// (Büstiyer → Bustiyer). Eşleşmeyen yeni ürün <see cref="UnmappedProductGroupCode"/> grubuna alınır.
    /// </summary>
    public Dictionary<string, string> ProductGroupCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kot Ceket"] = "kot_ceket",
        ["Büstiyer"] = "grp_9"
    };

    /// <summary>
    /// Hiçbir eşleşme bulunamayan ERP grubundaki YENİ ürünün alınacağı özelliksiz "geçici" grup kodu
    /// (2026-09-07 kullanıcı kararı: ürün atlanmaz, geçici gruba alınır; sözlüğe eşlenmemiş satır düşer,
    /// panel "Eşlenmemiş" kuyruğunda görünür; eşleme ve ürün güncellemesi personel işidir). Boş → eski
    /// fail-closed davranış (ürün atlanır). Mevcut ürünün grubuna hiçbir durumda dokunulmaz.
    /// </summary>
    public string? UnmappedProductGroupCode { get; set; } = "gecici";

    /// <summary>
    /// Birebir ad/kod eşleşmesi bulunamadığında kullanılan kontrollü ERP ürün grubu ailesi eşlemeleri.
    /// Anahtar yalnız tam kelime veya "anahtar + boşluk" öneki olarak eşleşir; en uzun tekil kural kazanır.
    /// </summary>
    public Dictionary<string, string> ProductGroupPrefixCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // 2026-09-07: Triko/Tesettür Triko → grp_14 önek kuralları KALDIRILDI — her Triko alt grubu kendi grubudur (kullanıcı kararı).

    public string? ResolveProductGroupPrefixCode(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return null;
        var normalized = ErpSourceSyncService.Normalize(sourceName);
        var matches = ProductGroupPrefixCodes
            .Select(x => (Prefix: ErpSourceSyncService.Normalize(x.Key), x.Value))
            .Where(x => normalized == x.Prefix || normalized.StartsWith(x.Prefix + " ", StringComparison.Ordinal))
            .OrderByDescending(x => x.Prefix.Length)
            .ToArray();
        if (matches.Length == 0) return null;

        var longestLength = matches[0].Prefix.Length;
        var targetCodes = matches
            .TakeWhile(x => x.Prefix.Length == longestLength)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return targetCodes.Length == 1 ? targetCodes[0] : null;
    }

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
        if (UsePanelGroupMappings && ProductAttributeTypeCodes.Values.Any(
                x => string.Equals(x, "urun_grubu", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Panel modunda urun_grubu yalnız grup varsayılanından gelir; ERP özellik eşlemesiyle ezilemez.");
        if (UsePanelGroupMappings && (string.IsNullOrWhiteSpace(MappingTargetSystem)
            || !MappingTargetSystem.StartsWith("erp:", StringComparison.Ordinal) || MappingTargetSystem.Length <= 4
            || MappingTargetSystem != MappingTargetSystem.Trim()))
            throw new InvalidOperationException("Panel grup eşlemesi hedefi erp:<servis> biçiminde olmalıdır.");
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
        if (string.IsNullOrWhiteSpace(SupplierAccountCodePrefix) || SupplierAccountCodePrefix.Length > 40)
            throw new InvalidOperationException("ErpSource supplier account code prefix 1-40 karakter olmalı.");
        if (SupplierReconciliationMinutes is < 5 or > 1440)
            throw new InvalidOperationException("ErpSource:SupplierReconciliationMinutes 5-1440 aralığında olmalı.");
        if (ProductGroupCodes.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("ErpSource product group mapping anahtar/değerleri boş olamaz.");
        if (ProductGroupPrefixCodes.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Value)))
            throw new InvalidOperationException("ErpSource product group prefix mapping anahtar/değerleri boş olamaz.");

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
