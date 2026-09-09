using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCardMessages;

/// <summary>
/// Kart mesajları DataGrid şeması (2026-09-09, tur 12).
///
/// ★ AYRI UÇ: mevcut <c>GET /storefront/card-messages</c> kanalın TAM listesini döner; sayfalamak
/// mesaj rotasyonunu okuyan yerleri kırar. Liste ekranı <c>/storefront/card-messages/grid</c> kullanır.
///
/// <para>Y3 (K2): liste tek kanal parametresine kilitli olsa bile şema <c>.Kanal(FirmPlatformId)</c>
/// BİLDİRİR ve kapsam ayrıca uygulanır — "kanal kolonu olan her şema kapsam bildirir" değişmezi
/// (KanalKapsamiTests). Controller'daki <c>[KanalKapsamiKontrol]</c> parametreyi denetler; bu ise
/// sorgunun kendisini kapsamla sınırlar.</para>
///
/// <para><c>yayinda</c>: <see cref="CardMessage.IsLiveAt"/> ile AYNI kural — aktif + tarih penceresi
/// içinde. Kural iki yerde yazılmasın diye şemadaki ifade o metodun sözleşmesini izler; mesajın
/// sitede görünüp görünmediği ekrandan tek süzgeçle sorulabilir.</para>
/// </summary>
public static class CardMessageGrid
{
    public static readonly string[] ScopeTypes = { "all", "category", "products" };
    public static readonly string[] Colors = { "yesil", "turuncu", "bordo", "pembe" };

    public static readonly GridSchema<CardMessage> Schema = new GridSchema<CardMessage>()
        .Kanal(m => m.FirmPlatformId)   // Y3 (K2)
        .Text("message", m => GridJson.Text(m.MessageI18n, "tr"))
        .Text("icon", m => m.Icon)
        .Enum("color", m => m.Color, Colors, nullToken: "yok")
        .Enum("scopeType", m => m.ScopeType, ScopeTypes)
        .Bool("isActive", m => m.IsActive)
        .Bool("ikonlu", m => m.Icon != null && m.Icon != "")
        .Bool("suresiz", m => m.StartDate == null && m.EndDate == null)
        .Bool("yayinda", m => m.IsActive
            && (m.StartDate == null || m.StartDate <= DateTime.UtcNow)
            && (m.EndDate == null || m.EndDate >= DateTime.UtcNow))
        .Number("slot", m => m.Slot)
        .Number("sortOrder", m => m.SortOrder)
        .Date("startDate", m => m.StartDate)
        .Date("endDate", m => m.EndDate)
        .Date("createdAt", m => m.CreatedAt)
        .Sort("message", m => GridJson.Text(m.MessageI18n, "tr"))
        .Sort("icon", m => m.Icon)
        .Sort("color", m => m.Color)
        .Sort("scopeType", m => m.ScopeType)
        .Sort("isActive", m => m.IsActive)
        .Sort("slot", m => m.Slot)
        .Sort("sortOrder", m => m.SortOrder)
        .Sort("startDate", m => m.StartDate)
        .Sort("endDate", m => m.EndDate)
        .Sort("createdAt", m => m.CreatedAt)
        // Eski ekranın sırası: alan (slot) → sıra → oluşturma.
        .DefaultSort(m => m.Slot, desc: false)
        .TieBreaker(m => m.SortOrder);

    public static IQueryable<CardMessage> ApplyNamed(IQueryable<CardMessage> query, CardMessageFiltreleri f)
    {
        query = query.Where(m => m.FirmPlatformId == f.FirmPlatformId);
        if (f.Slot is not null) query = query.Where(m => m.Slot == f.Slot);
        if (f.YalnizAktif) query = query.Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var t = f.Arama.Trim().ToLower();
            query = query.Where(m => GridJson.Text(m.MessageI18n, "tr")!.ToLower().Contains(t)
                || (m.Icon != null && m.Icon.ToLower().Contains(t)));
        }
        return query;
    }

    /// <param name="kanalKisiti">Y3 kapsam; verilmezse <c>grid.KanalKisiti</c>.</param>
    public static IQueryable<CardMessage> ApplyAll(
        IQueryable<CardMessage> query, CardMessageFiltreleri f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);
}

public record CardMessageFiltreleri(
    Guid FirmPlatformId, int? Slot = null, bool YalnizAktif = false, string? Arama = null);

public record GetCardMessagesGridQuery(
    CardMessageFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null,
    IReadOnlyCollection<Guid>? KanalKisiti = null)
    : IRequest<Result<PagedResult<CardMessageDto>>>;

public class GetCardMessagesGridQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetCardMessagesGridQuery, Result<PagedResult<CardMessageDto>>>
{
    public async Task<Result<PagedResult<CardMessageDto>>> Handle(GetCardMessagesGridQuery r, CancellationToken ct)
    {
        var q = CardMessageGrid.ApplyAll(db.CardMessages.AsNoTracking(), r.Filtreler, r.Grid, r.KanalKisiti);
        var toplam = await q.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE
        var satirlar = await CardMessageGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(m => new CardMessageDto(m.Id, m.Slot, m.MessageI18n, m.Icon, m.Color,
                m.ScopeType, m.ScopeCategoryIds, m.ScopeProductCodes,
                m.StartDate, m.EndDate, m.SortOrder, m.IsActive))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<CardMessageDto>(satirlar, toplam, r.Page, r.PageSize));
    }
}

public record CardMessageExportRow(
    int Slot, string? Mesaj, string? Icon, string? Color, string ScopeType, int KapsamAdet,
    DateTime? StartDate, DateTime? EndDate, int SortOrder, bool IsActive);

public record ExportCardMessagesQuery(
    CardMessageFiltreleri Filtreler, GridRequest Grid, int MaxRows,
    IReadOnlyCollection<Guid>? KanalKisiti = null)
    : IRequest<Result<GridExportSource<CardMessageExportRow>>>;

public class ExportCardMessagesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportCardMessagesQuery, Result<GridExportSource<CardMessageExportRow>>>
{
    public async Task<Result<GridExportSource<CardMessageExportRow>>> Handle(ExportCardMessagesQuery r, CancellationToken ct)
    {
        var q = CardMessageGrid.ApplyAll(db.CardMessages.AsNoTracking(), r.Filtreler, r.Grid, r.KanalKisiti);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CardMessageExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = CardMessageGrid.Schema.ApplySort(q, r.Grid).Select(m => new CardMessageExportRow(
            m.Slot, GridJson.Text(m.MessageI18n, "tr"), m.Icon, m.Color, m.ScopeType,
            m.ScopeType == "category" ? (m.ScopeCategoryIds == null ? 0 : m.ScopeCategoryIds.Count)
                : m.ScopeType == "products" ? (m.ScopeProductCodes == null ? 0 : m.ScopeProductCodes.Count)
                : 0,
            m.StartDate, m.EndDate, m.SortOrder, m.IsActive));
        return Result.Success(new GridExportSource<CardMessageExportRow>(count, rows));
    }
}
