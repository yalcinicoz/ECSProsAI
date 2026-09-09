using ECSPros.Inventory.Application.Queries.GetTransfers;

namespace ECSPros.Api.Grid;

/// <summary>Depo transferleri Excel kolonları (kod kilitli).</summary>
public static class TransferExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<TransferExportRow>> All = new GridExportColumn<TransferExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("fromWarehouse", "Çıkış Deposu", r => r.FromWarehouse),
        new("toWarehouse", "Varış Deposu", r => r.ToWarehouse),
        new("transferType", "Tip", r => r.TransferType),
        new("status", "Durum", r => TransferGrid.StatusLabel(r.Status)),
        new("itemCount", "Kalem", r => r.ItemCount),
        new("notes", "Not", r => r.Notes),
        new("requestedAt", "Talep", r => GridExportWriter.ToIstanbul(r.RequestedAt)),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
