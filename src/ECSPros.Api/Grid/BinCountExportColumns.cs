using ECSPros.Inventory.Application.Shelf;

namespace ECSPros.Api.Grid;

/// <summary>Raf sayımları Excel kolonları (FAZ 15.3).</summary>
public static class BinCountExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<BinCountExportRow>> All =
    [
        new("warehouse", "Depo", r => r.WarehouseCode),
        new("binCode", "Göz", r => r.BinCode),
        new("binBarcode", "Göz Barkodu", r => r.BinBarcode),
        new("status", "Durum", r => BinCountGrid.StatusLabel(r.Status)),
        new("startedAt", "Başlangıç", r => GridExportWriter.ToIstanbul(r.StartedAt)),
        new("finishedAt", "Bitiş", r => r.FinishedAt.HasValue ? GridExportWriter.ToIstanbul(r.FinishedAt.Value) : null),
        new("appliedAt", "Uygulama", r => r.AppliedAt.HasValue ? GridExportWriter.ToIstanbul(r.AppliedAt.Value) : null),
        new("expectedTotal", "Beklenen", r => r.ExpectedTotal),
        new("countedTotal", "Sayılan", r => r.CountedTotal),
        new("diffTotal", "Fark", r => r.DiffTotal),
        new("notes", "Not", r => r.Notes),
    ];
}
