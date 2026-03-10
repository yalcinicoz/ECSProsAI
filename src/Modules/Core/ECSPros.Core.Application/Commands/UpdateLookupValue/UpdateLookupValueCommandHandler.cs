using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateLookupValue;

public class UpdateLookupValueCommandHandler : IRequestHandler<UpdateLookupValueCommand, Result<bool>>
{
    private readonly ICoreDbContext _context;

    public UpdateLookupValueCommandHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateLookupValueCommand request, CancellationToken cancellationToken)
    {
        var lookupValue = await _context.LookupValues
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        if (lookupValue is null)
            return Result.Failure<bool>("Lookup değeri bulunamadı.");

        lookupValue.NameI18n = request.NameI18n;
        lookupValue.Color = request.Color;
        lookupValue.Icon = request.Icon;
        lookupValue.IsDefault = request.IsDefault;
        lookupValue.IsActive = request.IsActive;
        lookupValue.SortOrder = request.SortOrder;
        lookupValue.UpdatedAt = DateTime.UtcNow;
        lookupValue.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
