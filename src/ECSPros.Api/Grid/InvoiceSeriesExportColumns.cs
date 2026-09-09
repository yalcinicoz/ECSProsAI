using ECSPros.Order.Application.Queries.GetInvoiceSeries;

namespace ECSPros.Api.Grid;

/// <summary>Fatura serileri Excel kolonları (seri ön eki kilitli — numara akışının kimliği).</summary>
public static class InvoiceSeriesExportColumns
{
    private static string TipEtiketi(string t) => t switch
    {
        "e_archive" => "e-Arşiv", "e_invoice" => "e-Fatura", "export" => "İhracat", _ => t,
    };

    public static readonly IReadOnlyList<GridExportColumn<InvoiceSeriesExportRow>> All = new GridExportColumn<InvoiceSeriesExportRow>[]
    {
        new("serial", "Seri", r => r.Serial, Locked: true),
        new("invoiceType", "Tip", r => TipEtiketi(r.InvoiceType)),
        new("ad", "Ad", r => r.Ad),
        new("aciklama", "Açıklama", r => r.Aciklama),
        new("sozlesme", "Entegratör Sözleşmesi", r => r.Sozlesme),
        new("kanalSayisi", "Bağlı Kanal", r => r.KanalSayisi),
        new("sonYil", "Son Yıl", r => r.SonYil),
        new("sonSira", "Son Numara", r => r.SonSira),
        new("sonFatura", "Son Fatura Tarihi", r => r.SonFatura),
        new("aktif", "Aktif", r => r.Aktif ? "Evet" : "Hayır"),
        new("pasifeAlma", "Pasife Alma", r => r.PasifeAlma),
    };
}
