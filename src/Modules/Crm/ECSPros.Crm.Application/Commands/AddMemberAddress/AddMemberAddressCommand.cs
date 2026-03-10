using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.AddMemberAddress;

public record AddMemberAddressCommand(
    Guid MemberId,
    string Title,
    Guid? CountryId,
    string CountryName,
    Guid? CityId,
    string CityName,
    Guid? DistrictId,
    string DistrictName,
    Guid? NeighborhoodId,
    string? NeighborhoodName,
    string? AddressLine,
    string? PostalCode,
    string RecipientName,
    string RecipientPhone,
    string? DeliveryNotes,
    bool IsDefault
) : IRequest<Result<Guid>>;

public class AddMemberAddressCommandHandler : IRequestHandler<AddMemberAddressCommand, Result<Guid>>
{
    private readonly ICrmDbContext _db;

    public AddMemberAddressCommandHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddMemberAddressCommand request, CancellationToken ct)
    {
        var memberExists = await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct);
        if (!memberExists)
            return Result.Failure<Guid>("Üye bulunamadı.");

        if (request.IsDefault)
        {
            var existing = await _db.Addresses.Where(a => a.MemberId == request.MemberId && a.IsDefault).ToListAsync(ct);
            foreach (var addr in existing) addr.IsDefault = false;
        }

        var address = new Address
        {
            Id = Guid.NewGuid(),
            MemberId = request.MemberId,
            Title = request.Title,
            CountryId = request.CountryId,
            CountryName = request.CountryName,
            CityId = request.CityId,
            CityName = request.CityName,
            DistrictId = request.DistrictId,
            DistrictName = request.DistrictName,
            NeighborhoodId = request.NeighborhoodId,
            NeighborhoodName = request.NeighborhoodName,
            AddressLine = request.AddressLine,
            PostalCode = request.PostalCode,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            DeliveryNotes = request.DeliveryNotes,
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        _db.Addresses.Add(address);
        await _db.SaveChangesAsync(ct);

        return Result.Success(address.Id);
    }
}
