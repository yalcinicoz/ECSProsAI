using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberAddresses;

public record GetMemberAddressesQuery(Guid MemberId) : IRequest<Result<List<MemberAddressDto>>>;

public record MemberAddressDto(
    Guid Id,
    string Title,
    string? CountryName,
    string? CityName,
    string? DistrictName,
    string? NeighborhoodName,
    string? AddressLine,
    string? PostalCode,
    string RecipientName,
    string RecipientPhone,
    bool IsDefault);

public class GetMemberAddressesQueryHandler : IRequestHandler<GetMemberAddressesQuery, Result<List<MemberAddressDto>>>
{
    private readonly ICrmDbContext _db;

    public GetMemberAddressesQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<List<MemberAddressDto>>> Handle(GetMemberAddressesQuery request, CancellationToken ct)
    {
        var items = await _db.Addresses
            .Where(a => a.MemberId == request.MemberId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new MemberAddressDto(
                a.Id, a.Title,
                a.CountryName, a.CityName, a.DistrictName, a.NeighborhoodName,
                a.AddressLine, a.PostalCode,
                a.RecipientName, a.RecipientPhone, a.IsDefault))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
