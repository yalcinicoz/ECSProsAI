using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.SetDefaultMemberAddress;

/// <summary>E3: adresi varsayılan yap — üyenin önceki varsayılanları düşer
/// (sepet/ödeme adımları varsayılan adresi otomatik seçer).</summary>
public record SetDefaultMemberAddressCommand(Guid MemberId, Guid AddressId) : IRequest<Result<bool>>;

public class SetDefaultMemberAddressCommandHandler(ICrmDbContext db)
    : IRequestHandler<SetDefaultMemberAddressCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetDefaultMemberAddressCommand request, CancellationToken ct)
    {
        var adresler = await db.Addresses
            .Where(a => a.MemberId == request.MemberId && (a.Id == request.AddressId || a.IsDefault))
            .ToListAsync(ct);

        var hedef = adresler.FirstOrDefault(a => a.Id == request.AddressId);
        if (hedef is null)
            return Result.Failure<bool>("Adres bulunamadı.");

        foreach (var adres in adresler) adres.IsDefault = adres.Id == hedef.Id;
        hedef.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
