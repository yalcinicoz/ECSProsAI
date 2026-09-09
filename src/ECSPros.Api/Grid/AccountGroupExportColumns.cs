using ECSPros.Accounts.Application.Queries.GetAccountGroups;

namespace ECSPros.Api.Grid;

/// <summary>Cari grupları Excel kolonları (kod kilitli).</summary>
public static class AccountGroupExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<AccountGroupExportRow>> All = new GridExportColumn<AccountGroupExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("groupType", "Tip", r => AccountGroupGrid.TypeLabel(r.GroupType)),
        new("description", "Açıklama", r => r.Description),
        new("accountCount", "Cari Sayısı", r => r.AccountCount),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
