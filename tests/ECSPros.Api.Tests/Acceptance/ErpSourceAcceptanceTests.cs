using Microsoft.Data.SqlClient;
using ECSPros.Api.Services.ErpSource;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using System.Text.RegularExpressions;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[DoNotParallelize]
public sealed class ErpSourceAcceptanceTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] RequiredProcedures =
    [
        "jld_Appurunler",
        "jld_AppurunVaryantlari",
        "jld_ProductAttribute",
        "jld_V3Kategori"
    ];

    [TestMethod]
    public async Task ErpSource_ReadOnlyConnection_Acilir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "ECSPros-Acceptance-Tests",
            ConnectTimeout = 10
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT 1", connection)
        {
            CommandTimeout = 10
        };

        Assert.AreEqual(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [TestMethod]
    public async Task ErpSource_GerekliOkumaProsedurleri_Mevcut()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "ECSPros-Acceptance-Tests",
            ConnectTimeout = 10
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT p.name
            FROM sys.procedures p
            WHERE p.name IN (@p0, @p1, @p2, @p3)
            """, connection)
        {
            CommandTimeout = 10
        };

        for (var i = 0; i < RequiredProcedures.Length; i++)
            command.Parameters.AddWithValue($"@p{i}", RequiredProcedures[i]);

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            found.Add(reader.GetString(0));

        var missing = RequiredProcedures.Where(name => !found.Contains(name)).ToArray();
        Assert.AreEqual(0, missing.Length,
            $"ERP kaynak veritabanında eksik prosedürler: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public async Task ErpSource_OutboundAdayProsedurSozlesmelerini_Raporlar()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "ECSPros-Outbound-Contract-Audit",
            ConnectTimeout = 10
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT SCHEMA_NAME(p.schema_id), p.name, pr.name, TYPE_NAME(pr.user_type_id), pr.is_output
              FROM sys.procedures p
              LEFT JOIN sys.parameters pr ON pr.object_id=p.object_id AND pr.parameter_id>0
             WHERE LOWER(p.name) LIKE 'jld[_]%'
                OR LOWER(p.name) LIKE 'ecs[_]%'
                OR LOWER(p.name) LIKE '%ticimax%'
             ORDER BY p.name,pr.parameter_id
            """, connection) { CommandTimeout = 30 };
        var procedures = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!procedures.TryGetValue(name, out var parameters)) procedures[name] = parameters = [];
            if (!reader.IsDBNull(2))
                parameters.Add($"{reader.GetString(2)}:{reader.GetString(3)}{(reader.GetBoolean(4) ? ":out" : "")}");
        }
        foreach (var procedure in procedures)
            TestContext.WriteLine($"{procedure.Key}({string.Join(", ", procedure.Value)})");
        TestContext.WriteLine($"outboundCandidateCount={procedures.Count}");
    }

    [TestMethod]
    public async Task ErpSource_KatalogOkumaProsedurleri_SaltOkunurRaporlanir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "ECSPros-Catalog-Read-Contract-Audit",
            ConnectTimeout = 10
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT p.name,m.definition
              FROM sys.procedures p
              JOIN sys.sql_modules m ON m.object_id=p.object_id
             WHERE p.name IN (N'jld_Appurunler',N'jld_AppurunVaryantlari',N'jld_ProductAttribute')
             ORDER BY p.name;
            """, connection) { CommandTimeout = 30 };

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var definition = reader.IsDBNull(1) ? null : reader.GetString(1);
            found.Add(name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(definition), $"{name} tanımı okunamadı.");
            TestContext.WriteLine($"definition-begin:{name}");
            TestContext.WriteLine(definition);
            TestContext.WriteLine($"definition-end:{name}");
        }

        CollectionAssert.AreEquivalent(RequiredProcedures.Take(3).ToArray(), found.ToArray());
    }

    [TestMethod]
    public async Task ErpSource_KatalogFiyatVaryant_SozlesmesiOkunur()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");

        var source = new SqlServerErpSourceReader(new ErpSourceOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        });

        var products = await source.ReadProductsAsync(DateTime.UtcNow.AddDays(-2), CancellationToken.None);
        Assert.IsNotEmpty(products, "Son iki günde ERP katalog prosedürü ürün döndürmedi.");
        Assert.IsTrue(products.All(x => !string.IsNullOrWhiteSpace(x.Code)));
        Assert.IsTrue(products.All(x => x.BasePrice >= 0));

        IReadOnlyList<ErpVariantRow> variants = [];
        ErpProductRow? productWithVariant = null;
        foreach (var product in products.Take(20))
        {
            variants = await source.ReadVariantsAsync(product.Code, CancellationToken.None);
            if (variants.Count == 0) continue;
            productWithVariant = product;
            break;
        }

        Assert.IsNotNull(productWithVariant, "İlk 20 ERP ürününde varyant bulunamadı.");
        Assert.IsTrue(variants.All(x => !string.IsNullOrWhiteSpace(x.Barcode)));

        var attributes = await source.ReadProductAttributesAsync(
            productWithVariant.Code,
            CancellationToken.None);
        var snapshot = await source.ReadProductSnapshotAsync(productWithVariant.Code, CancellationToken.None);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(productWithVariant.Code, snapshot.Product.Code, true);
        Assert.IsNotEmpty(snapshot.Variants);
        var resolvedCode = await source.ResolveProductCodeByBarcodeAsync(
            snapshot.Variants[0].Barcode, CancellationToken.None);
        Assert.AreEqual(productWithVariant.Code, resolvedCode, true);

        TestContext.WriteLine(
            $"Ürün={products.Count}; varyant={variants.Count}; ürünÖzelliği={attributes.Count}; " +
            $"tedarikçi={(snapshot.Supplier is null ? "yok" : "var")}; " +
            $"grupDolu={products.Count(x => !string.IsNullOrWhiteSpace(x.ProductGroupName))}; " +
            $"satışFiyatıPozitif={products.Count(x => x.BasePrice > 0)}");
    }

    [TestMethod]
    public async Task ErpSource_SonDegisikliklerinAttributeKeywordEnvanterini_Raporlar()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var source = new SqlServerErpSourceReader(new ErpSourceOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        });

        var products = await source.ReadProductsAsync(DateTime.UtcNow.AddDays(-2), CancellationToken.None);
        Assert.IsNotEmpty(products, "Son iki günde ERP katalog prosedürü ürün döndürmedi.");

        var rows = new List<(string ProductCode, string KeywordId, string Value)>();
        foreach (var product in products.Take(100))
        {
            var attributes = await source.ReadProductAttributesAsync(product.Code, CancellationToken.None);
            rows.AddRange(attributes.Select(x => (product.Code, x.KeywordId, x.Value)));
        }

        foreach (var group in rows.GroupBy(x => x.KeywordId, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var values = group.Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Take(12);
            TestContext.WriteLine(
                $"keywordId={group.Key}; products={group.Select(x => x.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; " +
                $"rows={group.Count()}; sampleValues={string.Join(" | ", values)}");
        }

        TestContext.WriteLine($"sourceProducts={products.Count}; scannedProducts={Math.Min(products.Count, 100)}; " +
                              $"attributeRows={rows.Count}; keywordCount={rows.Select(x => x.KeywordId).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    }

    [TestMethod]
    public async Task ErpSource_HedefliUrunSnapshotVeBarkodCozumu_Okunur()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var requestedProductCode = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_ERP_PRODUCT_CODE")?.Trim();
        string productCode;
        if (!string.IsNullOrWhiteSpace(requestedProductCode))
        {
            productCode = requestedProductCode;
        }
        else await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                SELECT TOP (1) b.ItemCode
                  FROM prItemBarcode b WITH (NOLOCK)
                  JOIN prItemVariant v WITH (NOLOCK)
                    ON v.ItemCode=b.ItemCode AND v.ColorCode=b.ColorCode AND v.ItemDim1Code=b.ItemDim1Code
                   AND v.ItemTypeCode=1
                  JOIN cdColorDesc c WITH (NOLOCK) ON c.ColorCode=v.ColorCode
                  JOIN cdItem i WITH (NOLOCK) ON i.ItemCode=b.ItemCode AND i.ItemTypeCode=1
                 WHERE NULLIF(LTRIM(RTRIM(b.Barcode)),'') IS NOT NULL
                 ORDER BY i.LastUpdatedDate DESC
                """, connection) { CommandTimeout = 30 };
            productCode = Convert.ToString(await command.ExecuteScalarAsync())?.Trim()
                          ?? throw new AssertFailedException("V3 örnek ürün kodu bulunamadı.");
        }

        var source = new SqlServerErpSourceReader(new ErpSourceOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        });
        var snapshot = await source.ReadProductSnapshotAsync(productCode, CancellationToken.None);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(productCode, snapshot.Product.Code, true);
        var expectedKeywordId = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_ERP_KEYWORD_ID")?.Trim();
        var expectedKeywordValue = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_ERP_KEYWORD_VALUE")?.Trim();
        if (!string.IsNullOrWhiteSpace(expectedKeywordId) && !string.IsNullOrWhiteSpace(expectedKeywordValue))
        {
            var expected = snapshot.Attributes.FirstOrDefault(x =>
                    string.Equals(x.KeywordId, expectedKeywordId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Value, expectedKeywordValue, StringComparison.CurrentCultureIgnoreCase));
            Assert.IsNotNull(expected,
                $"V3 ürün özelliği beklenen değerde değil: keywordId={expectedKeywordId}, value={expectedKeywordValue}.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected.SourceCode),
                $"V3 ürün özelliğinin kararlı kaynak kodu okunmadı: keywordId={expectedKeywordId}.");
        }
        Assert.IsNotEmpty(snapshot.Variants);
        Assert.IsTrue(snapshot.Variants.SelectMany(x => x.Attributes)
            .Any(x => x.TypeId == 1 && !string.IsNullOrWhiteSpace(x.SourceCode)),
            "Renk kararlı V3 ColorCode ile okunmadı.");
        var resolved = await source.ResolveProductCodeByBarcodeAsync(
            snapshot.Variants[0].Barcode, CancellationToken.None);
        Assert.AreEqual(productCode, resolved, true);
        TestContext.WriteLine(
            $"product={productCode}; group={snapshot.Product.ProductGroupName}; " +
            $"variants={snapshot.Variants.Count}; attributes={snapshot.Attributes.Count}; " +
            $"supplier={(snapshot.Supplier is null ? "none" : snapshot.Supplier.Code)}; " +
            $"attributes={snapshot.Attributes.Count}");
        TestContext.WriteLine("keyword20Values=" + string.Join(" | ", snapshot.Attributes
            .Where(x => string.Equals(x.KeywordId, "20", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)));
        TestContext.WriteLine("mappedAttributes=" + string.Join(" | ", snapshot.Attributes
            .Select(x => $"{x.KeywordId}:{x.SourceCode}={x.Value}")));
    }

    [TestMethod]
    public async Task ErpSource_HedefliUrunTumAttributeEnvanteri_Okunur()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var productCode = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_ERP_PRODUCT_CODE")?.Trim();
        Assert.IsFalse(string.IsNullOrWhiteSpace(productCode),
            "ECSPROS_ACCEPTANCE_ERP_PRODUCT_CODE zorunlu.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT a.AttributeTypeCode,t.AttributeTypeDescription,a.AttributeCode,d.AttributeDescription
              FROM prItemAttribute a WITH (NOLOCK)
              JOIN cdItemAttributeTypeDesc t WITH (NOLOCK)
                ON t.AttributeTypeCode=a.AttributeTypeCode AND t.ItemTypeCode=1 AND t.LangCode='TR'
              JOIN cdItemAttributeDesc d WITH (NOLOCK)
                ON d.AttributeTypeCode=a.AttributeTypeCode AND d.AttributeCode=a.AttributeCode
               AND d.ItemTypeCode=1 AND d.LangCode='TR'
             WHERE a.ItemCode=@code
             ORDER BY a.AttributeTypeCode,a.AttributeCode
            """, connection) { CommandTimeout = 120 };
        command.Parameters.Add(new SqlParameter("@code", SqlDbType.VarChar, 20) { Value = productCode });

        var count = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            count++;
            TestContext.WriteLine(
                $"type={Convert.ToInt32(reader.GetValue(0))}; typeName={reader.GetString(1)}; " +
                $"valueCode={reader.GetString(2)}; value={reader.GetString(3)}");
        }
        Assert.IsGreaterThan(0, count, $"V3 ürün attribute'u bulunamadı: {productCode}");
    }

    [TestMethod]
    public async Task ErpSource_ProductFeatureAttributeTypeEnvanteri_Okunur()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT a.AttributeTypeCode,t.AttributeTypeDescription,COUNT_BIG(*)
              FROM prItemAttribute a WITH (NOLOCK)
              JOIN cdItemAttributeTypeDesc t WITH (NOLOCK)
                ON t.AttributeTypeCode=a.AttributeTypeCode AND t.ItemTypeCode=1 AND t.LangCode='TR'
             WHERE a.AttributeTypeCode BETWEEN 16 AND 37
             GROUP BY a.AttributeTypeCode,t.AttributeTypeDescription
             ORDER BY a.AttributeTypeCode
            """, connection) { CommandTimeout = 120 };

        var count = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            count++;
            TestContext.WriteLine(
                $"type={Convert.ToInt32(reader.GetValue(0))}; typeName={reader.GetString(1)}; rows={reader.GetInt64(2)}");
        }
        Assert.IsGreaterThan(0, count, "V3 ürün feature attribute tipi bulunamadı.");
    }

    [TestMethod]
    public async Task ErpSource_FiyatKolonlarini_MevcutHedefleKarsilastirir()
    {
        var erpConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "ERP hedef PostgreSQL bağlantısı");
        var options = new ErpSourceOptions
        {
            ConnectionString = erpConnection,
            CommandTimeoutSeconds = 120
        };
        var source = new SqlServerErpSourceReader(options);
        var products = await source.ReadProductsAsync(DateTime.UtcNow.AddDays(-2), CancellationToken.None);
        Assert.IsNotEmpty(products, "Son iki günde ERP katalog prosedürü ürün döndürmedi.");

        string[] columns =
        [
            "tozluSatisFiyati", "tozluListeFiyati", "juludeSatisFiyati",
            "juludeListeFiyati", "BayiSatisFiyati"
        ];
        var baseMatches = columns.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        var channelMatches = columns.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        int targetFound = 0, singleChannelPrice = 0;

        await using var dataSource = NpgsqlDataSource.Create(targetConnection);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT p."BasePrice",
                   (SELECT min(cv."Price")
                      FROM catalog.product_variants v
                      JOIN storefront.channel_variants cv ON cv."VariantId"=v."Id" AND NOT cv."IsDeleted"
                      JOIN core.core_firm_platforms fp ON fp."Id"=cv."FirmPlatformId" AND fp."Code"='mishar'
                     WHERE v."ProductId"=p."Id" AND NOT v."IsDeleted"),
                   (SELECT max(cv."Price")
                      FROM catalog.product_variants v
                      JOIN storefront.channel_variants cv ON cv."VariantId"=v."Id" AND NOT cv."IsDeleted"
                      JOIN core.core_firm_platforms fp ON fp."Id"=cv."FirmPlatformId" AND fp."Code"='mishar'
                     WHERE v."ProductId"=p."Id" AND NOT v."IsDeleted")
              FROM catalog.products p
             WHERE p."Code"=@code AND NOT p."IsDeleted"
            """, connection);
        var codeParameter = command.Parameters.Add("code", NpgsqlTypes.NpgsqlDbType.Text);

        foreach (var product in products)
        {
            codeParameter.Value = product.Code;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) continue;
            targetFound++;
            var basePrice = reader.GetDecimal(0);
            decimal? channelMin = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
            decimal? channelMax = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
            if (channelMin.HasValue && channelMin == channelMax) singleChannelPrice++;

            foreach (var column in columns)
            {
                if (!product.Values.TryGetValue(column, out var value) || !value.HasValue) continue;
                if (value.Value == basePrice) baseMatches[column]++;
                if (channelMin.HasValue && channelMin == channelMax && value.Value == channelMin.Value)
                    channelMatches[column]++;
            }
        }

        foreach (var column in columns)
            TestContext.WriteLine($"column={column}; baseMatches={baseMatches[column]}; channelMatches={channelMatches[column]}");
        TestContext.WriteLine($"sourceProducts={products.Count}; targetFound={targetFound}; singleChannelPrice={singleChannelPrice}");
    }

    [TestMethod]
    public async Task ErpSource_ProductAttributeMappingFarklarini_Raporlar()
    {
        var erpConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "ERP hedef PostgreSQL bağlantısı");
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "yil",
            ["6"] = "marka",
            ["10"] = "cinsiyet",
            ["20"] = "kalip",
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
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [$"6|{Normalize("julude.com")}"] = "julude"
        };

        var sourceValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var connection = new SqlConnection(erpConnection))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                SELECT CONVERT(nvarchar(20),a.AttributeTypeCode),d.AttributeDescription
                  FROM prItemAttribute a WITH (NOLOCK)
                  JOIN cdItemAttributeDesc d WITH (NOLOCK)
                    ON d.AttributeTypeCode=a.AttributeTypeCode
                   AND d.AttributeCode=a.AttributeCode
                   AND d.LangCode='TR' AND d.ItemTypeCode=1
                 WHERE a.AttributeTypeCode IN (1,6,10,20,44,45,51,53,54,55,56,57,71)
                 GROUP BY a.AttributeTypeCode,d.AttributeDescription
                """, connection) { CommandTimeout = 120 };
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var sourceCode = reader.GetString(0);
                if (!sourceValues.TryGetValue(sourceCode, out var values))
                    sourceValues[sourceCode] = values = new(StringComparer.OrdinalIgnoreCase);
                values.Add(reader.GetString(1));
            }
        }

        var targetValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var dataSource = NpgsqlDataSource.Create(targetConnection))
        await using (var command = dataSource.CreateCommand("""
            SELECT t."Code",COALESCE(v."NameI18n"->>'tr','')
              FROM definition.attribute_types t
              LEFT JOIN definition.attribute_values v
                ON v."AttributeTypeId"=t."Id" AND NOT v."IsDeleted"
             WHERE NOT t."IsDeleted"
            """))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var targetCode = reader.GetString(0);
                if (!targetValues.TryGetValue(targetCode, out var values))
                    targetValues[targetCode] = values = new(StringComparer.OrdinalIgnoreCase);
                if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1)))
                    values.Add(Normalize(reader.GetString(1)));
            }
        }

        foreach (var mapping in mappings.OrderBy(x => int.Parse(x.Key)))
        {
            sourceValues.TryGetValue(mapping.Key, out var source);
            targetValues.TryGetValue(mapping.Value, out var target);
            source ??= new(StringComparer.OrdinalIgnoreCase);
            target ??= new(StringComparer.OrdinalIgnoreCase);
            var missing = source.Where(x =>
                !target.Contains(Normalize(aliases.GetValueOrDefault($"{mapping.Key}|{Normalize(x)}", x))))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            TestContext.WriteLine(
                $"keywordId={mapping.Key}; target={mapping.Value}; sourceValues={source.Count}; " +
                $"targetValues={target.Count}; missing={missing.Length}; " +
                $"missingValues={string.Join(" | ", missing.Take(50))}");
        }
    }

    [TestMethod]
    public async Task ErpTarget_Postgres_ReadOnlyHazirlikKontrolu()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "ERP hedef PostgreSQL bağlantısı");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "ECSPros-ERP-ReadOnly-Check",
            Timeout = 10,
            CommandTimeout = 30
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT
                current_database(),
                to_regclass('integration.erp_sync_checkpoints') IS NOT NULL,
                CASE WHEN to_regclass('definition.product_groups') IS NULL THEN -1
                     ELSE (SELECT count(*) FROM definition.product_groups WHERE "IsDeleted"=false) END,
                CASE WHEN to_regclass('catalog.products') IS NULL THEN -1
                     ELSE (SELECT count(*) FROM catalog.products WHERE "IsDeleted"=false) END
            """, connection);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        TestContext.WriteLine(
            $"database={reader.GetString(0)}; checkpointTable={reader.GetBoolean(1)}; " +
            $"productGroups={reader.GetInt64(2)}; products={reader.GetInt64(3)}");
    }

    [TestMethod]
    public async Task ErpSource_KatalogVeFiyat_DryRunYazmadanRaporlanir()
    {
        var erpConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_SOURCE",
            "ConnectionStrings:ErpSource",
            "ERP kaynak SQL Server bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "ERP hedef PostgreSQL bağlantısı");

        var options = new ErpSourceOptions
        {
            ConnectionString = erpConnection,
            DryRun = true,
            InitialSinceUtc = DateTime.UtcNow.AddDays(-2),
            OverlapMinutes = 0,
            CommandTimeoutSeconds = 120,
        };

        await using var dataSource = NpgsqlDataSource.Create(targetConnection);
        var service = new ErpSourceSyncService(
            dataSource,
            new SqlServerErpSourceReader(options),
            options,
            NullLogger<ErpSourceSyncService>.Instance);

        var before = await CountProductsAsync(dataSource);
        var catalog = await service.SyncCatalogAsync(CancellationToken.None);
        var price = await service.SyncPricesAsync(CancellationToken.None);
        var after = await CountProductsAsync(dataSource);

        Assert.IsTrue(catalog.Success, catalog.Error);
        Assert.IsTrue(price.Success, price.Error);
        Assert.IsTrue(catalog.DryRun);
        Assert.IsTrue(price.DryRun);
        Assert.AreEqual(before, after, "Dry-run hedef katalogda ürün yazdı.");
        TestContext.WriteLine(
            $"catalogChanged={catalog.Changed}; catalogMappingError={catalog.Error is not null}; " +
            $"priceChanged={price.Changed}; targetProductsBefore={before}; targetProductsAfter={after}; " +
            $"catalogMs={catalog.DurationMs}; priceMs={price.DurationMs}");
        TestContext.WriteLine("Eksik gruplar: " + ExtractDistinct(catalog.Detail, "ERP grubu '([^']+)'"));
        TestContext.WriteLine("Eksik tanım değerleri: " + ExtractDistinct(catalog.Detail, "Tanım değeri yok: ([^\\r\\n]+)"));
        TestContext.WriteLine("Eksik varyant tipleri: " + ExtractDistinct(catalog.Detail, "ERP typeId=([0-9]+)"));
        await WriteMappingSuggestionsAsync(dataSource, catalog.Detail);
    }

    private static async Task<long> CountProductsAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM catalog.products WHERE \"IsDeleted\"=false");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string ExtractDistinct(string input, string pattern)
    {
        var values = Regex.Matches(input, pattern, RegexOptions.CultureInvariant)
            .Select(x => x.Groups[1].Value.Trim().TrimEnd('.'))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "yok" : string.Join(", ", values);
    }

    private async Task WriteMappingSuggestionsAsync(NpgsqlDataSource dataSource, string detail)
    {
        var missingGroups = Regex.Matches(detail, "ERP grubu '([^']+)'", RegexOptions.CultureInvariant)
            .Select(x => x.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var groups = new List<(string Code, string Name)>();
        await using (var command = dataSource.CreateCommand(
                         "SELECT \"Code\",COALESCE(\"NameI18n\"->>'tr','') FROM definition.product_groups WHERE \"IsDeleted\"=false"))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) groups.Add((reader.GetString(0), reader.GetString(1)));

        foreach (var source in missingGroups.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var candidates = groups.OrderBy(x => Distance(Normalize(source), Normalize(x.Name)))
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(x => $"{x.Code}:{x.Name}");
            TestContext.WriteLine($"Grup önerisi | {source} -> {string.Join(" | ", candidates)}");
        }

        var missingValues = Regex.Matches(detail, "Tanım değeri yok: ([^=]+)='([^']+)'", RegexOptions.CultureInvariant)
            .Select(x => (Type: x.Groups[1].Value.Trim(), Value: x.Groups[2].Value.Trim()))
            .Distinct()
            .ToArray();
        var values = new List<(string Type, string Value)>();
        await using (var command = dataSource.CreateCommand("""
                         SELECT t."Code",COALESCE(v."NameI18n"->>'tr','')
                         FROM definition.attribute_values v
                         JOIN definition.attribute_types t ON t."Id"=v."AttributeTypeId"
                         WHERE v."IsDeleted"=false AND t."IsDeleted"=false AND t."Code" IN ('renk','beden')
                         """))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) values.Add((reader.GetString(0), reader.GetString(1)));

        foreach (var source in missingValues.OrderBy(x => x.Type).ThenBy(x => x.Value))
        {
            var candidates = values.Where(x => x.Type.Equals(source.Type, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => Distance(Normalize(source.Value), Normalize(x.Value)))
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(x => x.Value);
            TestContext.WriteLine($"Tanım önerisi | {source.Type}='{source.Value}' -> {string.Join(" | ", candidates)}");
        }
    }

    private static string Normalize(string value)
        => string.Concat(value.Trim().ToLower(new System.Globalization.CultureInfo("tr-TR"))
            .Where(char.IsLetterOrDigit));

    private static int Distance(string left, string right)
    {
        var costs = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var diagonal = costs[0];
            costs[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var above = costs[j];
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    diagonal + (left[i - 1] == right[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return costs[right.Length];
    }

}
