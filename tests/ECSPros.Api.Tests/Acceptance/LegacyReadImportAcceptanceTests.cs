using ECSPros.Api.Services.LegacyImport;
using ECSPros.Crm.Infrastructure.Persistence;
using ECSPros.Integration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using Npgsql;

namespace ECSPros.Api.Tests.Acceptance;

[TestClass]
[TestCategory("Acceptance")]
[DoNotParallelize]
public sealed class LegacyReadImportAcceptanceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LegacyMySql_UrunGorselMetadata_SaltOkunurKontrolEdilir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var productCode = Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_LEGACY_PRODUCT_CODE")?.Trim();
        if (string.IsNullOrWhiteSpace(productCode))
            Assert.Inconclusive("ECSPROS_ACCEPTANCE_LEGACY_PRODUCT_CODE verilmedi.");

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var readOnly = new MySqlCommand("SET TRANSACTION READ ONLY", connection))
            await readOnly.ExecuteNonQueryAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new MySqlCommand("""
            SELECT p.Id,COUNT(r.Id),COUNT(DISTINCT IFNULL(r.resimSetId,1))
              FROM apurunler p
              LEFT JOIN apurunresimleri r
                ON r.urunId=p.Id AND r.isSilindi=0
               AND r.resimDosyaAdi IS NOT NULL AND r.resimDosyaAdi<>''
             WHERE p.urunKodu=@code
             GROUP BY p.Id
            """, connection, transaction) { CommandTimeout = 120 };
        command.Parameters.AddWithValue("@code", productCode);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync(), $"Legacy MySQL ürünü bulunamadı: {productCode}");
        var imageCount = Convert.ToInt32(reader.GetValue(1));
        var imageSetCount = Convert.ToInt32(reader.GetValue(2));
        Assert.IsGreaterThan(0, imageCount, $"Legacy MySQL aktif görsel metadata'sı bulunamadı: {productCode}");
        TestContext.WriteLine($"product={productCode}; images={imageCount}; imageSets={imageSetCount}");
        await reader.DisposeAsync();
        await transaction.RollbackAsync();
    }

    [TestMethod]
    public async Task LegacyMySql_Probe_ReadOnlyTransactionIcindeCalisir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var source = new MySqlLegacyReadSource(new LegacyReadImportOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        });

        var probe = await source.ProbeAsync(41, CancellationToken.None);

        Assert.IsFalse(string.IsNullOrWhiteSpace(probe.Database));
        Assert.IsFalse(string.IsNullOrWhiteSpace(probe.Version));
        Assert.IsGreaterThanOrEqualTo(104L, probe.MemberCount);
        Assert.IsGreaterThanOrEqualTo(71L, probe.OrderCount);
        Assert.IsGreaterThanOrEqualTo(45L, probe.InvoiceCount);
        Assert.IsGreaterThanOrEqualTo(12L, probe.ReturnCount);
        TestContext.WriteLine(
            $"database={probe.Database}; platform={probe.PlatformId}; members={probe.MemberCount}; " +
            $"orders={probe.OrderCount}; invoices={probe.InvoiceCount}; returns={probe.ReturnCount}");
    }

    [TestMethod]
    public async Task LegacySiparisAggregate_SaltOkunurSnapshotTutarlidir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        };
        var source = new MySqlLegacyReadSource(options);
        var aggregateReader = new LegacyOrderAggregateReader(source);

        var snapshot = await aggregateReader.ReadAsync(41, CancellationToken.None);

        Assert.IsGreaterThanOrEqualTo(71, snapshot.Orders.Count);
        Assert.IsGreaterThanOrEqualTo(181, snapshot.Lines.Count);
        Assert.IsGreaterThanOrEqualTo(72, snapshot.Payments.Count);
        Assert.HasCount(snapshot.Orders.Count, snapshot.Orders.Select(x => x.Id).Distinct());
        Assert.HasCount(snapshot.Lines.Count, snapshot.Lines.Select(x => x.Id).Distinct());
        Assert.HasCount(snapshot.Payments.Count, snapshot.Payments.Select(x => x.Id).Distinct());
        Assert.IsTrue(snapshot.Lines.All(x => snapshot.Orders.Any(o => o.Id == x.OrderId)));
        Assert.IsTrue(snapshot.Payments.All(x => snapshot.Orders.Any(o => o.Id == x.OrderId)));
        Assert.IsTrue(snapshot.Orders.All(x => LegacyOrderStatusMapper.Map(x.RawStatus) is not null));
        TestContext.WriteLine(
            $"orders={snapshot.Orders.Count}; lines={snapshot.Lines.Count}; payments={snapshot.Payments.Count}; " +
            $"addresses={snapshot.Addresses.Count}");
    }

    [TestMethod]
    public async Task LegacyFaturaVeIade_SaltOkunurSnapshotTutarlidir()
    {
        var connectionString = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = connectionString,
            CommandTimeoutSeconds = 120
        };
        var source = new MySqlLegacyReadSource(options);
        var invoices = await new LegacyInvoiceReader(source).ReadAsync(41, CancellationToken.None);
        var returns = await new LegacyReturnReader(source).ReadAsync(41, CancellationToken.None);

        Assert.IsGreaterThanOrEqualTo(45, invoices.Count);
        Assert.IsTrue(invoices.All(x => LegacyInvoiceNumberParser.Parse(x.InvoiceNumber) is not null));
        Assert.HasCount(invoices.Count, invoices.Select(x => x.Id).Distinct());
        Assert.IsGreaterThanOrEqualTo(12, returns.Returns.Count);
        Assert.IsGreaterThanOrEqualTo(28, returns.Items.Count);
        Assert.HasCount(returns.Returns.Count, returns.Returns.Select(x => x.Id).Distinct());
        Assert.HasCount(returns.Items.Count, returns.Items.Select(x => x.Id).Distinct());
        Assert.IsTrue(returns.Items.All(x => returns.Returns.Any(r => r.Id == x.ReturnId)));
        var amountMismatchCount = returns.Returns.Count(r =>
            Math.Abs(r.ReturnAmount - returns.Items.Where(x => x.ReturnId == r.Id).Sum(x => x.Amount)) > 0.02m);
        Assert.IsTrue(returns.Items.All(x => x.OrderLineQuantity > 0));
        TestContext.WriteLine(
            $"invoices={invoices.Count}; returns={returns.Returns.Count}; " +
            $"returnItems={returns.Items.Count}; returnLogs={returns.Logs.Count}; " +
            $"amountMismatches={amountMismatchCount}");
    }

    [TestMethod]
    public async Task LegacyUyeAdres_DryRun_HedefiDegistirmez()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            DryRun = true,
            MembersEnabled = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var crmOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(dataSource).Options;
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(dataSource).Options;
        await using var crm = new CrmDbContext(crmOptions);
        await using var integration = new IntegrationDbContext(integrationOptions);
        var source = new MySqlLegacyReadSource(options);
        var reader = new LegacyMemberAddressReader(source);
        var checkpoints = new LegacyImportCheckpointStore(integration);
        var slice = new LegacyMemberAddressImportSlice(
            reader, crm, checkpoints, options, NullLogger<LegacyMemberAddressImportSlice>.Instance);

        var before = await TargetFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsTrue(report.DryRun);
        Assert.IsGreaterThan(0, report.Changed);
        Assert.AreEqual(before, after, "Üye/adres dry-run hedef PostgreSQL'i değiştirdi.");
        TestContext.WriteLine($"changed={report.Changed}; skipped={report.Skipped}; members={before.Members}; addresses={before.Addresses}");
    }

    [TestMethod]
    public async Task LegacyUyeAdres_KontrolluGercekAktarim()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        await RequireLegacyTargetWriteAsync(targetConnection);

        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            DryRun = false,
            MembersEnabled = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var crmOptions = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(dataSource).Options;
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(dataSource).Options;
        await using var crm = new CrmDbContext(crmOptions);
        await using var integration = new IntegrationDbContext(integrationOptions);
        var source = new MySqlLegacyReadSource(options);
        var slice = new LegacyMemberAddressImportSlice(
            new LegacyMemberAddressReader(source), crm,
            new LegacyImportCheckpointStore(integration), options,
            NullLogger<LegacyMemberAddressImportSlice>.Instance);

        var before = await TargetFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsFalse(report.DryRun);
        Assert.IsGreaterThanOrEqualTo(before.Members, after.Members);
        Assert.IsGreaterThanOrEqualTo(before.Addresses, after.Addresses);
        Assert.IsGreaterThanOrEqualTo(before.LegacyAddresses, after.LegacyAddresses);
        Assert.IsGreaterThan(0, after.LegacyAddresses,
            "Gerçek aktarım sonrasında LegacyAddressId bağlı adres oluşmadı.");
        TestContext.WriteLine(
            $"changed={report.Changed}; skipped={report.Skipped}; " +
            $"members={before.Members}->{after.Members}; addresses={before.Addresses}->{after.Addresses}; " +
            $"legacyAddresses={before.LegacyAddresses}->{after.LegacyAddresses}");
    }

    [TestMethod]
    public async Task LegacySiparis_DryRun_HedefiDegistirmez()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = true,
            OrdersEnabled = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var source = new MySqlLegacyReadSource(options);
        var aggregateReader = new LegacyOrderAggregateReader(source);
        var slice = new LegacyOrderImportSlice(
            aggregateReader, dataSource, new DryRunCheckpointStore(), options,
            NullLogger<LegacyOrderImportSlice>.Instance);

        var before = await TargetOrderFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetOrderFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsTrue(report.DryRun);
        Assert.IsGreaterThan(0, report.Changed);
        Assert.AreEqual(before, after, "Sipariş dry-run hedef PostgreSQL'i değiştirdi.");
        TestContext.WriteLine(
            $"changed={report.Changed}; skipped={report.Skipped}; orders={before.Orders}; " +
            $"items={before.Items}; payments={before.Payments}");
    }

    [TestMethod]
    public async Task LegacySiparis_KontrolluGercekAktarim()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        await RequireLegacyTargetWriteAsync(targetConnection);

        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = false,
            OrdersEnabled = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(dataSource).Options;
        await using var integration = new IntegrationDbContext(integrationOptions);
        var slice = new LegacyOrderImportSlice(
            new LegacyOrderAggregateReader(new MySqlLegacyReadSource(options)),
            dataSource, new LegacyImportCheckpointStore(integration), options,
            NullLogger<LegacyOrderImportSlice>.Instance);

        var before = await TargetOrderFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetOrderFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsFalse(report.DryRun);
        Assert.IsGreaterThanOrEqualTo(before.Orders, after.Orders);
        Assert.IsGreaterThanOrEqualTo(before.Items, after.Items);
        Assert.IsGreaterThanOrEqualTo(before.Payments, after.Payments);
        Assert.IsGreaterThan(0, after.Payments,
            "Gerçek sipariş aktarımı sonrasında ödeme kaydı oluşmadı.");
        TestContext.WriteLine(
            $"changed={report.Changed}; skipped={report.Skipped}; " +
            $"orders={before.Orders}->{after.Orders}; items={before.Items}->{after.Items}; " +
            $"payments={before.Payments}->{after.Payments}");
    }

    [TestMethod]
    public async Task LegacyFaturaVeIade_DryRun_HedefiDegistirmez()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = true,
            InvoicesEnabled = true,
            ReturnsEnabled = true,
            ReturnAmountMismatchPolicy = LegacyReturnAmountMismatchPolicies.UseItemTotal
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var source = new MySqlLegacyReadSource(options);
        var invoiceSlice = new LegacyInvoiceImportSlice(
            new LegacyInvoiceReader(source), dataSource, new DryRunCheckpointStore(), options,
            NullLogger<LegacyInvoiceImportSlice>.Instance);
        var returnSlice = new LegacyReturnImportSlice(
            new LegacyReturnReader(source), dataSource, new DryRunCheckpointStore(), options,
            NullLogger<LegacyReturnImportSlice>.Instance);

        var before = await TargetCommerceFingerprintAsync(targetConnection);
        var invoiceReport = await invoiceSlice.RunAsync(CancellationToken.None);
        var returnReport = await returnSlice.RunAsync(CancellationToken.None);
        var after = await TargetCommerceFingerprintAsync(targetConnection);

        Assert.IsTrue(invoiceReport.DryRun);
        Assert.IsTrue(returnReport.DryRun);
        Assert.IsTrue(invoiceReport.Success, invoiceReport.Error);
        Assert.IsTrue(returnReport.Success, returnReport.Error);
        Assert.AreEqual(before, after, "Fatura/iade dry-run hedef PostgreSQL'i değiştirdi.");
        TestContext.WriteLine(
            $"invoiceSuccess={invoiceReport.Success}; invoiceChanged={invoiceReport.Changed}; " +
            $"invoiceSkipped={invoiceReport.Skipped}; invoiceError={invoiceReport.Error ?? "-"}");
        TestContext.WriteLine(
            $"returnSuccess={returnReport.Success}; returnChanged={returnReport.Changed}; " +
            $"returnSkipped={returnReport.Skipped}; returnError={returnReport.Error ?? "-"}");
    }

    [TestMethod]
    public async Task LegacyFatura_KontrolluGercekAktarim()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        await RequireLegacyTargetWriteAsync(targetConnection);

        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = false,
            InvoicesEnabled = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(dataSource).Options;
        await using var integration = new IntegrationDbContext(integrationOptions);
        var slice = new LegacyInvoiceImportSlice(
            new LegacyInvoiceReader(new MySqlLegacyReadSource(options)), dataSource,
            new LegacyImportCheckpointStore(integration), options,
            NullLogger<LegacyInvoiceImportSlice>.Instance);

        var before = await TargetInvoiceFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetInvoiceFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsFalse(report.DryRun);
        Assert.IsGreaterThanOrEqualTo(before.Invoices, after.Invoices);
        Assert.IsGreaterThanOrEqualTo(before.Items, after.Items);
        Assert.IsGreaterThan(0, after.Invoices, "Gerçek fatura aktarımı sonrasında fatura oluşmadı.");
        TestContext.WriteLine(
            $"changed={report.Changed}; skipped={report.Skipped}; " +
            $"invoices={before.Invoices}->{after.Invoices}; items={before.Items}->{after.Items}");
    }

    [TestMethod]
    public async Task LegacyFaturaSerileri_MsrVeTya_KontrolluOlarakHazirlanir()
    {
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        await RequireLegacyTargetWriteAsync(targetConnection);
        if (!bool.TryParse(
                Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_LEGACY_ALLOW_SERIES_CREATE"),
                out var allowSeriesCreate) || !allowSeriesCreate)
        {
            Assert.Inconclusive(
                "MSR/TYA serisi için ECSPROS_ACCEPTANCE_LEGACY_ALLOW_SERIES_CREATE=true açıkça verilmelidir.");
        }

        await using var connection = new NpgsqlConnection(targetConnection);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var firmCommand = new NpgsqlCommand("""
            SELECT DISTINCT "FirmId"
              FROM "order".ord_invoice_series
             WHERE NOT "IsDeleted" AND "IsActive"
               AND ('TST' = upper("EArchiveSerial")
                 OR 'TST' = upper("EInvoiceSerial")
                 OR 'TST' = upper("ExportSerial"))
            """, connection, transaction);
        var firmIds = new List<Guid>();
        await using (var firmReader = await firmCommand.ExecuteReaderAsync())
        {
            while (await firmReader.ReadAsync()) firmIds.Add(firmReader.GetGuid(0));
        }
        Assert.HasCount(1, firmIds, "TST serisinden tek bir hedef firma çözümlenemedi.");

        var inserted = new List<string>();
        foreach (var prefix in new[] { "MSR", "TYA" })
        {
            await using var checkCommand = new NpgsqlCommand("""
                SELECT count(*),
                       count(*) FILTER (
                           WHERE "FirmId"=@firmId AND NOT "IsDeleted" AND "IsActive"
                             AND upper("EArchiveSerial")=@prefix
                             AND upper("EInvoiceSerial")=@prefix
                             AND upper("ExportSerial")=@prefix)
                  FROM "order".ord_invoice_series
                 WHERE upper("EArchiveSerial")=@prefix
                    OR upper("EInvoiceSerial")=@prefix
                    OR upper("ExportSerial")=@prefix
                """, connection, transaction);
            checkCommand.Parameters.AddWithValue("firmId", firmIds[0]);
            checkCommand.Parameters.AddWithValue("prefix", prefix);
            await using var checkReader = await checkCommand.ExecuteReaderAsync();
            Assert.IsTrue(await checkReader.ReadAsync());
            var total = checkReader.GetInt64(0);
            var valid = checkReader.GetInt64(1);
            await checkReader.DisposeAsync();
            Assert.IsLessThanOrEqualTo(1, total, $"{prefix} birden fazla fatura serisinde kullanılıyor.");
            if (total == 1)
            {
                Assert.AreEqual(1L, valid, $"Mevcut {prefix} serisi hedef firma/alan/aktiflik sözleşmesine uymuyor.");
                continue;
            }

            await using var insertCommand = new NpgsqlCommand("""
                INSERT INTO "order".ord_invoice_series
                    ("Id","FirmId","Name","EArchiveSerial","EInvoiceSerial","ExportSerial",
                     "IsActive","CreatedAt","IsDeleted")
                VALUES (gen_random_uuid(),@firmId,@name,@prefix,@prefix,@prefix,true,now(),false)
                """, connection, transaction);
            insertCommand.Parameters.AddWithValue("firmId", firmIds[0]);
            insertCommand.Parameters.AddWithValue("name", $"Legacy {prefix} Import Serisi");
            insertCommand.Parameters.AddWithValue("prefix", prefix);
            Assert.AreEqual(1, await insertCommand.ExecuteNonQueryAsync());
            inserted.Add(prefix);
        }
        await transaction.CommitAsync();
        TestContext.WriteLine(
            $"seriesReady=MSR,TYA; inserted={(inserted.Count == 0 ? "none" : string.Join(',', inserted))}");
    }

    [TestMethod]
    public async Task LegacyIade_KontrolluGercekAktarim_KalemToplaminiKullanir()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        await RequireLegacyTargetWriteAsync(targetConnection);

        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = false,
            ReturnsEnabled = true,
            ReturnAmountMismatchPolicy = LegacyReturnAmountMismatchPolicies.UseItemTotal
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection)
            .EnableDynamicJson()
            .Build();
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(dataSource).Options;
        await using var integration = new IntegrationDbContext(integrationOptions);
        var slice = new LegacyReturnImportSlice(
            new LegacyReturnReader(new MySqlLegacyReadSource(options)), dataSource,
            new LegacyImportCheckpointStore(integration), options,
            NullLogger<LegacyReturnImportSlice>.Instance);

        var before = await TargetReturnFingerprintAsync(targetConnection);
        var report = await slice.RunAsync(CancellationToken.None);
        var after = await TargetReturnFingerprintAsync(targetConnection);

        Assert.IsTrue(report.Success, report.Error);
        Assert.IsFalse(report.DryRun);
        Assert.IsGreaterThanOrEqualTo(before.Returns, after.Returns);
        Assert.IsGreaterThanOrEqualTo(before.Items, after.Items);
        Assert.IsGreaterThan(0, after.Returns, "Gerçek iade aktarımı sonrasında iade oluşmadı.");
        TestContext.WriteLine(
            $"changed={report.Changed}; skipped={report.Skipped}; " +
            $"returns={before.Returns}->{after.Returns}; items={before.Items}->{after.Items}; " +
            $"amountPolicy={options.ReturnAmountMismatchPolicy}");
    }

    [TestMethod]
    public async Task LegacyTamUzlastirma_HedefiDegistirmezVeEksikleriRaporlar()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var options = new LegacyReadImportOptions
        {
            ConnectionString = legacyConnection,
            PlatformId = 41,
            FirmPlatformCode = "mishar",
            DryRun = true
        };
        await using var dataSource = new NpgsqlDataSourceBuilder(targetConnection).Build();
        var source = new MySqlLegacyReadSource(options);
        var service = new LegacyImportReconciliationService(
            new LegacyMemberAddressReader(source),
            new LegacyOrderAggregateReader(source),
            new LegacyInvoiceReader(source),
            new LegacyReturnReader(source),
            dataSource,
            options);

        var before = await TargetCommerceFingerprintAsync(targetConnection);
        var report = await service.RunAsync(CancellationToken.None);
        var after = await TargetCommerceFingerprintAsync(targetConnection);

        Assert.AreEqual(before, after, "Salt-okunur uzlaştırma hedef PostgreSQL'i değiştirdi.");
        Assert.HasCount(8, report.Entities);
        Assert.IsGreaterThanOrEqualTo(104, report.Entities.Single(x => x.Entity == "members").SourceCount);
        Assert.IsGreaterThanOrEqualTo(71, report.Entities.Single(x => x.Entity == "orders").SourceCount);
        Assert.IsGreaterThanOrEqualTo(45, report.Entities.Single(x => x.Entity == "invoices").SourceCount);
        Assert.IsGreaterThanOrEqualTo(12, report.Entities.Single(x => x.Entity == "returns").SourceCount);
        var missingAddressIds = report.Entities.Single(x => x.Entity == "addresses").MissingSourceIds;
        TestContext.WriteLine(
            $"complete={report.IsComplete}; missing={report.TotalMissing}; " +
            string.Join(", ", report.Entities.Select(x => $"{x.Entity}:{x.TargetMatchedCount}/{x.SourceCount}")) +
            $"; missingAddressIds=[{string.Join(',', missingAddressIds)}]");
    }

    [TestMethod]
    public async Task LegacyAktarimSonrasi_FaturaSerileriVeIadeKalemToplamlariTutarlidir()
    {
        var legacyConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_LEGACY_READ",
            "LegacyReadImport:ConnectionString",
            "legacy MySQL SELECT-only bağlantısı");
        var targetConnection = AcceptanceTestEnvironment.Require(
            "ECSPROS_ACCEPTANCE_ERP_TARGET",
            "ConnectionStrings:DefaultConnection",
            "hedef PostgreSQL bağlantısı");
        var options = new LegacyReadImportOptions { ConnectionString = legacyConnection, PlatformId = 41 };
        var source = new MySqlLegacyReadSource(options);
        var invoices = await new LegacyInvoiceReader(source).ReadAsync(41, CancellationToken.None);
        var returns = await new LegacyReturnReader(source).ReadAsync(41, CancellationToken.None);

        await using var connection = new NpgsqlConnection(targetConnection);
        await connection.OpenAsync();
        var targetInvoiceSerials = new Dictionary<int, string>();
        await using (var invoiceCommand = new NpgsqlCommand("""
            SELECT "LegacyInvoiceId","InvoiceSerial"
              FROM "order".ord_invoices
             WHERE "LegacyInvoiceId"=ANY(@ids) AND NOT "IsDeleted"
            """, connection))
        {
            invoiceCommand.Parameters.AddWithValue("ids", invoices.Select(x => x.Id).ToArray());
            await using var invoiceReader = await invoiceCommand.ExecuteReaderAsync();
            while (await invoiceReader.ReadAsync())
                targetInvoiceSerials[invoiceReader.GetInt32(0)] = invoiceReader.GetString(1);
        }
        foreach (var invoice in invoices)
        {
            var parsed = LegacyInvoiceNumberParser.Parse(invoice.InvoiceNumber);
            Assert.IsNotNull(parsed);
            Assert.IsTrue(targetInvoiceSerials.TryGetValue(invoice.Id, out var targetSerial));
            Assert.AreEqual(parsed.Serial, targetSerial, true, $"Fatura {invoice.Id} serisi uyuşmuyor.");
        }

        var targetReturns = new Dictionary<int, (decimal RefundAmount, decimal ItemTotal)>();
        await using (var returnCommand = new NpgsqlCommand("""
            SELECT r."LegacyReturnId",r."RefundAmount",
                   coalesce(sum(i."TotalRefundAmount") FILTER (WHERE NOT i."IsDeleted"),0)
              FROM "order".ord_returns r
              LEFT JOIN "order".ord_return_items i ON i."ReturnId"=r."Id"
             WHERE r."LegacyReturnId"=ANY(@ids) AND NOT r."IsDeleted"
             GROUP BY r."LegacyReturnId",r."RefundAmount"
            """, connection))
        {
            returnCommand.Parameters.AddWithValue("ids", returns.Returns.Select(x => x.Id).ToArray());
            await using var returnReader = await returnCommand.ExecuteReaderAsync();
            while (await returnReader.ReadAsync())
                targetReturns[returnReader.GetInt32(0)] = (returnReader.GetDecimal(1), returnReader.GetDecimal(2));
        }
        foreach (var sourceReturn in returns.Returns)
        {
            Assert.IsTrue(targetReturns.TryGetValue(sourceReturn.Id, out var targetReturn));
            var expectedItemTotal = returns.Items.Where(x => x.ReturnId == sourceReturn.Id).Sum(x => x.Amount);
            Assert.IsLessThanOrEqualTo(
                0.02m, Math.Abs(targetReturn.RefundAmount - expectedItemTotal),
                $"İade {sourceReturn.Id} RefundAmount kalem toplamını kullanmıyor.");
            Assert.IsLessThanOrEqualTo(
                0.02m, Math.Abs(targetReturn.ItemTotal - expectedItemTotal),
                $"İade {sourceReturn.Id} hedef kalem toplamı kaynakla uyuşmuyor.");
        }

        var distribution = targetInvoiceSerials.Values
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}:{x.Count()}");
        TestContext.WriteLine(
            $"invoiceSeries={string.Join(',', distribution)}; " +
            $"returnsUsingItemTotal={targetReturns.Count}; amountMismatches=0");
    }

    private static async Task RequireLegacyTargetWriteAsync(string targetConnection)
    {
        if (!bool.TryParse(
                Environment.GetEnvironmentVariable("ECSPROS_ACCEPTANCE_LEGACY_ALLOW_TARGET_WRITE"),
                out var allowWrite) || !allowWrite)
        {
            Assert.Inconclusive(
                "Legacy hedef yazımı için ECSPROS_ACCEPTANCE_LEGACY_ALLOW_TARGET_WRITE=true açıkça verilmelidir.");
        }

        var expectedDatabase = Environment.GetEnvironmentVariable(
            "ECSPROS_ACCEPTANCE_LEGACY_EXPECTED_TARGET_DATABASE");
        var expectedServerAddress = Environment.GetEnvironmentVariable(
            "ECSPROS_ACCEPTANCE_LEGACY_EXPECTED_TARGET_SERVER");
        if (string.IsNullOrWhiteSpace(expectedDatabase) || string.IsNullOrWhiteSpace(expectedServerAddress))
        {
            Assert.Inconclusive(
                "Yazmalı test için beklenen hedef database ve server adresi açıkça verilmelidir.");
        }

        var targetBuilder = new NpgsqlConnectionStringBuilder(targetConnection);
        Assert.AreEqual(
            expectedDatabase, targetBuilder.Database, true,
            "Bağlantı dizesindeki hedef database güvenlik kapısıyla uyuşmuyor.");

        await using var identityConnection = new NpgsqlConnection(targetConnection);
        await identityConnection.OpenAsync();
        await using var identityCommand = new NpgsqlCommand("""
            SELECT current_database(), host(inet_server_addr()),
                   current_setting('transaction_read_only')
            """, identityConnection);
        await using var identityReader = await identityCommand.ExecuteReaderAsync();
        Assert.IsTrue(await identityReader.ReadAsync());
        Assert.AreEqual(expectedDatabase, identityReader.GetString(0), true);
        Assert.AreEqual(expectedServerAddress, identityReader.GetString(1), true,
            "PostgreSQL gerçek sunucu adresi güvenlik kapısıyla uyuşmuyor.");
        Assert.AreEqual("off", identityReader.GetString(2), true,
            "Hedef PostgreSQL yazılabilir değil.");
    }

    private static async Task<(long Members, long Addresses, long LegacyAddresses)> TargetFingerprintAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM crm.crm_members),
                   (SELECT count(*) FROM crm.crm_addresses),
                   (SELECT count(*) FROM crm.crm_addresses WHERE "LegacyAddressId" IS NOT NULL)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<(long Orders, long Items, long Payments)> TargetOrderFingerprintAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM "order".ord_orders),
                   (SELECT count(*) FROM "order".ord_order_items),
                   (SELECT count(*) FROM "order".ord_order_payments)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<(long Invoices, long Items)> TargetInvoiceFingerprintAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM "order".ord_invoices),
                   (SELECT count(*) FROM "order".ord_invoice_items)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<(long Returns, long Items)> TargetReturnFingerprintAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM "order".ord_returns),
                   (SELECT count(*) FROM "order".ord_return_items)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task<string> TargetCommerceFingerprintAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM crm.crm_members),
                   (SELECT count(*) FROM crm.crm_addresses),
                   (SELECT count(*) FROM "order".ord_orders),
                   (SELECT count(*) FROM "order".ord_order_items),
                   (SELECT count(*) FROM "order".ord_order_payments),
                   (SELECT count(*) FROM "order".ord_invoices),
                   (SELECT count(*) FROM "order".ord_invoice_items),
                   (SELECT count(*) FROM "order".ord_returns),
                   (SELECT count(*) FROM "order".ord_return_items),
                   (SELECT count(*) FROM integration.legacy_import_checkpoints)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        return string.Join('|', Enumerable.Range(0, 10).Select(reader.GetInt64));
    }

    private sealed class DryRunCheckpointStore : ILegacyImportCheckpointStore
    {
        public Task<LegacyImportCheckpointValue?> GetAsync(string slice, int platformId, CancellationToken ct) =>
            Task.FromResult<LegacyImportCheckpointValue?>(null);

        public Task SaveSuccessAsync(
            string slice, int platformId, DateTime watermarkUtc, long lastSourceId, CancellationToken ct) =>
            throw new AssertFailedException("Dry-run checkpoint başarı kaydı yazmamalı.");

        public Task SaveErrorAsync(string slice, int platformId, string error, CancellationToken ct) =>
            throw new AssertFailedException("Dry-run checkpoint hata kaydı yazmamalı.");
    }
}
