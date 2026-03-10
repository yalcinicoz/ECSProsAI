using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.DeleteMemberAddress;

public record DeleteMemberAddressCommand(Guid MemberId, Guid AddressId) : IRequest<Result<bool>>;

public class DeleteMemberAddressCommandHandler : IRequestHandler<DeleteMemberAddressCommand, Result<bool>>
{
    private readonly ICrmDbContext _db;

    public DeleteMemberAddressCommandHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteMemberAddressCommand request, CancellationToken ct)
    {
        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.MemberId == request.MemberId, ct);

        if (address is null)
            return Result.Failure<bool>("Adres bulunamadı.");

        address.IsDeleted = true;
        address.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
