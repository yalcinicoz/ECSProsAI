namespace ECSPros.Order.Domain.Entities;

/// <summary>Fatura tipleri — seri tanımı, kanal yuvası ve fatura kaydı aynı kümeyi kullanır.</summary>
public static class InvoiceTypes
{
    public const string EArchive = "e_archive";
    public const string EInvoice = "e_invoice";
    public const string Export = "export";

    public static readonly string[] All = [EArchive, EInvoice, Export];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);

    public static string Label(string type) => type switch
    {
        EArchive => "e-Arşiv",
        EInvoice => "e-Fatura",
        Export => "İhracat",
        _ => type
    };
}

/// <summary>Kanal fatura gönderim yöntemleri (docs/fatura-entegrasyon-plani.md §0.5).</summary>
public static class InvoiceSendMethods
{
    /// <summary>Yalnız kayıt; hiçbir yere gönderilmez (go-live öncesi varsayılan).</summary>
    public const string Manual = "manual";
    /// <summary>Entegratöre API ile biz göndeririz (FE3/FE4).</summary>
    public const string IntegratorApi = "integrator_api";
    /// <summary>ERP (Nebim V3) gönderir; numara ERP'den gelir (FE6 / E7).</summary>
    public const string Erp = "erp";
    /// <summary>Pazaryeri kendi keser; numara pazaryerinden gelir (FE7).</summary>
    public const string Marketplace = "marketplace";

    public static readonly string[] All = [Manual, IntegratorApi, Erp, Marketplace];
    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Fatura numarasının kaynağı (§0.6).</summary>
public static class InvoiceNumberSources
{
    public const string Internal = "internal";
    public const string Erp = "erp";
    public const string Marketplace = "marketplace";
    public const string Integrator = "integrator";
    public static readonly string[] All = [Internal, Erp, Marketplace, Integrator];
    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
