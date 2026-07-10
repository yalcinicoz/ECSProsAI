using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.UpdateMemberAddress;

/// <summary>E3: adres güncelleme (C4'te ertelenmişti) — yalnız adresin sahibi üye
/// güncelleyebilir; varsayılan işaretlenirse üyenin diğer varsayılanları düşer.</summary>
public record UpdateMemberAddressCommand(
    Guid MemberId,
    Guid AddressId,
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
) : IRequest<Result<bool>>;

public class UpdateMemberAddressCommandHandler(ICrmDbContext db)
    : IRequestHandler<UpdateMemberAddressCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateMemberAddressCommand request, CancellationToken ct)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(
            a => a.Id == request.AddressId && a.MemberId == request.MemberId, ct);
        if (address is null)
            return Result.Failure<bool>("Adres bulunamadı.");

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.RecipientName))
            return Result.Failure<bool>("Adres başlığı ve ad soyad zorunludur.");

        if (request.IsDefault && !address.IsDefault)
        {
            var digerVarsayilanlar = await db.Addresses
                .Where(a => a.MemberId == request.MemberId && a.IsDefault && a.Id != address.Id)
                .ToListAsync(ct);
            foreach (var diger in digerVarsayilanlar) diger.IsDefault = false;
        }

        address.Title = request.Title;
        address.CountryId = request.CountryId;
        address.CountryName = request.CountryName;
        address.CityId = request.CityId;
        address.CityName = request.CityName;
        address.DistrictId = request.DistrictId;
        address.DistrictName = request.DistrictName;
        address.NeighborhoodId = request.NeighborhoodId;
        address.NeighborhoodName = request.NeighborhoodName;
        address.AddressLine = request.AddressLine;
        address.PostalCode = request.PostalCode;
        address.RecipientName = request.RecipientName;
        address.RecipientPhone = request.RecipientPhone;
        address.DeliveryNotes = request.DeliveryNotes;
        address.IsDefault = request.IsDefault;
        address.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
