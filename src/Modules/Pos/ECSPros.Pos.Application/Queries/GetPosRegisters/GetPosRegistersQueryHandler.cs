using ECSPros.Pos.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Queries.GetPosRegisters;

public class GetPosRegistersQueryHandler : IRequestHandler<GetPosRegistersQuery, Result<List<PosRegisterDto>>>
{
    private readonly IPosDbContext _context;

    public GetPosRegistersQueryHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PosRegisterDto>>> Handle(GetPosRegistersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PosRegisters.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(r => r.IsActive);

        var items = await query
            .OrderBy(r => r.Code)
            .Select(r => new PosRegisterDto(r.Id, r.Code, r.Name, r.ReceiptPrefix, r.WarehouseId, r.FirmPlatformId, r.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
