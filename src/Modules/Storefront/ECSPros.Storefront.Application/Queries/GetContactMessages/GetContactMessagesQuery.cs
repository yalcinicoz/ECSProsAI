using ECSPros.Shared.Kernel.Common;
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
    int PageSize = 20) : IRequest<Result<PagedResult<ContactMessageDto>>>;

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
        var q = db.ContactMessages.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(m => m.Status == request.Status);
        if (request.FirmPlatformId.HasValue)
            q = q.Where(m => m.FirmPlatformId == request.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToLower();
            q = q.Where(m =>
                m.Name.ToLower().Contains(aranan) ||
                m.Email.ToLower().Contains(aranan) ||
                (m.Subject != null && m.Subject.ToLower().Contains(aranan)));
        }

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q
            .OrderByDescending(m => m.CreatedAt)
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
