using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetContactMessages;

/// <summary>P5: iletişim formu gelen kutusu (admin) — durum/platform filtreli, sayfalı.</summary>
public record GetContactMessagesQuery(
    string? Status = null,
    Guid? FirmPlatformId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    // Y3 (K2): kullanıcının görebileceği kanallar; null = kısıt yok, boş = hiçbir kayıt.
    IReadOnlyCollection<Guid>? KanalKisiti = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<ContactMessageDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (ContactMessageGrid.Schema)

public record ContactMessageDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid? MemberId,
    string Name,
    string Email,
    string? Phone,
    string? Subject,
    string Message,
    string Status,
    DateTime CreatedAt);

public class GetContactMessagesQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetContactMessagesQuery, Result<PagedResult<ContactMessageDto>>>
{
    public async Task<Result<PagedResult<ContactMessageDto>>> Handle(
        GetContactMessagesQuery request, CancellationToken ct)
    {
        // Y3 kanal kapsamı + adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var q = ContactMessageGrid.ApplyAll(
            db.ContactMessages.AsNoTracking(),
            new ContactMessageFilters(request.Status, request.FirmPlatformId, request.Search),
            request.Grid,
            request.KanalKisiti ?? request.Grid?.KanalKisiti);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await ContactMessageGrid.Schema.ApplySort(q, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new ContactMessageDto(
                m.Id, m.FirmPlatformId, m.MemberId, m.Name, m.Email,
                m.Phone, m.Subject, m.Message, m.Status, m.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ContactMessageDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
