using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateLookupValue;

public class CreateLookupValueCommandHandler : IRequestHandler<CreateLookupValueCommand, Result<Guid>>
{
    private readonly ICoreDbContext _context;

    public CreateLookupValueCommandHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateLookupValueCommand request, CancellationToken cancellationToken)
    {
        var type = await _context.LookupTypes
            .FirstOrDefaultAsync(t => t.Code == request.TypeCode, cancellationToken);

        if (type is null)
            return Result.Failure<Guid>($"Lookup type '{request.TypeCode}' bulunamadı.");

        var exists = await _context.LookupValues.AnyAsync(
            v => v.LookupTypeId == type.Id && v.Code == request.Code, cancellationToken);

        if (exists)
            return Result.Failure<Guid>($"Bu tipte '{request.Code}' kodu zaten mevcut.");

        var value = new LookupValue
        {
            LookupTypeId = type.Id,
            Code = request.Code,
            NameI18n = request.NameI18n,
            Color = request.Color,
            Icon = request.Icon,
            IsDefault = request.IsDefault,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _context.LookupValues.Add(value);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(value.Id);
    }
}
