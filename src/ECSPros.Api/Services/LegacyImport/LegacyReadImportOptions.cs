namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Production MySQL -> PostgreSQL geçici ticaret importu. Güvenli varsayılan olarak tamamen kapalıdır;
/// bağlantı dizesi yalnız environment/secret üzerinden verilir.
/// </summary>
public sealed class LegacyReadImportOptions
{
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public int PlatformId { get; set; } = 41;
    public string FirmPlatformCode { get; set; } = "mishar";
    public bool MembersEnabled { get; set; }
    public bool OrdersEnabled { get; set; }
    public bool InvoicesEnabled { get; set; }
    public bool ReturnsEnabled { get; set; }
    public bool ImagesEnabled { get; set; }
    public bool ImagesDryRun { get; set; } = true;
    public int MissingImagesIntervalMinutes { get; set; } = 10;
    public int MissingImagesBatchSize { get; set; } = 25;
    public int ImagesIntervalMinutes { get; set; } = 1440;
    public int ImagesFullStartupDelayMinutes { get; set; } = 60;
    public string ReturnAmountMismatchPolicy { get; set; } = LegacyReturnAmountMismatchPolicies.Block;
    public int IntervalSeconds { get; set; } = 120;
    public int StartupDelaySeconds { get; set; } = 90;
    public int OverlapMinutes { get; set; } = 30;
    public int FullReconciliationHourUtc { get; set; } = 2;
    public int CommandTimeoutSeconds { get; set; } = 120;
    public string SourceTimeZoneId { get; set; } = "Europe/Istanbul";
    public string ConnectionString { get; set; } = string.Empty;

    public IReadOnlyList<string> EnabledSlices()
    {
        var slices = new List<string>(6);
        if (MembersEnabled) slices.Add(LegacyImportSlices.Members);
        if (OrdersEnabled) slices.Add(LegacyImportSlices.Orders);
        if (InvoicesEnabled) slices.Add(LegacyImportSlices.Invoices);
        if (ReturnsEnabled) slices.Add(LegacyImportSlices.Returns);
        if (ImagesEnabled)
        {
            slices.Add(LegacyImportSlices.MissingImages);
            slices.Add(LegacyImportSlices.Images);
        }
        return slices;
    }

    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("LegacyReadImport etkin fakat ConnectionString boş.");
        if (PlatformId <= 0)
            throw new InvalidOperationException("LegacyReadImport:PlatformId pozitif olmalı.");
        if (string.IsNullOrWhiteSpace(FirmPlatformCode))
            throw new InvalidOperationException("LegacyReadImport:FirmPlatformCode boş olamaz.");
        if (EnabledSlices().Count == 0)
            throw new InvalidOperationException("LegacyReadImport etkin fakat hiçbir veri dilimi açık değil.");
        if (IntervalSeconds is < 30 or > 86400)
            throw new InvalidOperationException("LegacyReadImport:IntervalSeconds 30-86400 aralığında olmalı.");
        if (StartupDelaySeconds is < 0 or > 3600)
            throw new InvalidOperationException("LegacyReadImport:StartupDelaySeconds 0-3600 aralığında olmalı.");
        if (OverlapMinutes is < 0 or > 1440)
            throw new InvalidOperationException("LegacyReadImport:OverlapMinutes 0-1440 aralığında olmalı.");
        if (FullReconciliationHourUtc is < 0 or > 23)
            throw new InvalidOperationException("LegacyReadImport:FullReconciliationHourUtc 0-23 aralığında olmalı.");
        if (CommandTimeoutSeconds is < 5 or > 600)
            throw new InvalidOperationException("LegacyReadImport:CommandTimeoutSeconds 5-600 aralığında olmalı.");
        if (ImagesIntervalMinutes is < 30 or > 10080)
            throw new InvalidOperationException("LegacyReadImport:ImagesIntervalMinutes 30-10080 aralığında olmalı.");
        if (MissingImagesIntervalMinutes is < 3 or > 1440)
            throw new InvalidOperationException("LegacyReadImport:MissingImagesIntervalMinutes 3-1440 aralığında olmalı.");
        if (MissingImagesBatchSize is < 1 or > 100)
            throw new InvalidOperationException("LegacyReadImport:MissingImagesBatchSize 1-100 aralığında olmalı.");
        if (ImagesFullStartupDelayMinutes is < 0 or > 1440)
            throw new InvalidOperationException("LegacyReadImport:ImagesFullStartupDelayMinutes 0-1440 aralığında olmalı.");
        if (!LegacyReturnAmountMismatchPolicies.IsSupported(ReturnAmountMismatchPolicy))
            throw new InvalidOperationException(
                "LegacyReadImport:ReturnAmountMismatchPolicy yalnız Block veya UseItemTotal olabilir.");
    }
}

public static class LegacyReturnAmountMismatchPolicies
{
    public const string Block = "Block";
    public const string UseItemTotal = "UseItemTotal";

    public static bool IsSupported(string? value) =>
        string.Equals(value, Block, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, UseItemTotal, StringComparison.OrdinalIgnoreCase);
}

public static class LegacyImportSlices
{
    public const string Members = "members";
    public const string Orders = "orders";
    public const string Invoices = "invoices";
    public const string Returns = "returns";
    public const string Images = "images";
    public const string MissingImages = "images-missing";
}
