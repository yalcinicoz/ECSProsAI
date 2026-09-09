using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetContactMessages;

/// <summary>
/// İletişim mesajları DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (status/firmPlatformId) + global arama (ad, e-posta, konu).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — kapsam dışı kanalın mesajı listede/sayımda/Excel'de
/// görünmez. Kapsam liste ucunda parser'a, export'ta <c>body.ToGridRequest(kapsam)</c>'a verilir.</para>
/// <para>Ad/e-posta/telefon kişisel veridir: Excel kolonlarında alan yetkisiyle maskelenir.</para>
/// </summary>
public static class ContactMessageGrid
{
    public static readonly string[] Statuses = { "new", "read", "answered", "archived" };

    public static readonly GridSchema<ContactMessage> Schema = new GridSchema<ContactMessage>()
        .Kanal(m => m.FirmPlatformId)   // Y3 (K2)
        .Text("name", m => m.Name)
        .Text("email", m => m.Email)
        .Text("phone", m => m.Phone)
        .Text("subject", m => m.Subject)
        .Text("message", m => m.Message)
        .Enum("status", m => m.Status, Statuses)
        .Bool("isMember", m => m.MemberId != null)
        .Bool("hasPhone", m => m.Phone != null && m.Phone != "")
        .Date("createdAt", m => m.CreatedAt)
        .Guid("firmPlatformId", m => m.FirmPlatformId)
        .Guid("memberId", m => m.MemberId)
        .Sort("name", m => m.Name)
        .Sort("email", m => m.Email)
        .Sort("subject", m => m.Subject)
        .Sort("status", m => m.Status)
        .Sort("createdAt", m => m.CreatedAt)
        .DefaultSort(m => m.CreatedAt, desc: true)
        .TieBreaker(m => m.Id);

    public static IQueryable<ContactMessage> ApplyNamed(IQueryable<ContactMessage> query, ContactMessageFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(m => m.Status == f.Status);
        if (f.FirmPlatformId.HasValue) query = query.Where(m => m.FirmPlatformId == f.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var aranan = f.Search.Trim().ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(aranan) ||
                m.Email.ToLower().Contains(aranan) ||
                (m.Subject != null && m.Subject.ToLower().Contains(aranan)));
        }
        return query;
    }

    /// <param name="kanalKisiti">Y3 kapsam; verilmezse <c>grid.KanalKisiti</c>.</param>
    public static IQueryable<ContactMessage> ApplyAll(
        IQueryable<ContactMessage> query, ContactMessageFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);

    public static string StatusLabel(string s) => s switch
    {
        "new" => "Yeni", "read" => "Okundu", "answered" => "Yanıtlandı", "archived" => "Arşiv", _ => s,
    };
}

public record ContactMessageFilters(string? Status = null, Guid? FirmPlatformId = null, string? Search = null);

public record ContactMessageExportRow(
    string Name, string Email, string? Phone, string? Subject, string Message, string Status, DateTime CreatedAt);

public record ExportContactMessagesQuery(ContactMessageFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ContactMessageExportRow>>>;

public class ExportContactMessagesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportContactMessagesQuery, Result<GridExportSource<ContactMessageExportRow>>>
{
    public async Task<Result<GridExportSource<ContactMessageExportRow>>> Handle(
        ExportContactMessagesQuery r, CancellationToken ct)
    {
        var q = ContactMessageGrid.ApplyAll(db.ContactMessages.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ContactMessageExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = ContactMessageGrid.Schema.ApplySort(q, r.Grid).Select(m => new ContactMessageExportRow(
            m.Name, m.Email, m.Phone, m.Subject, m.Message, m.Status, m.CreatedAt));
        return Result.Success(new GridExportSource<ContactMessageExportRow>(count, rows));
    }
}
