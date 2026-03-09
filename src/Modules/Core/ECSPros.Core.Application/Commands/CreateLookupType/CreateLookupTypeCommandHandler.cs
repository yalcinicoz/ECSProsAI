using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateLookupType;

public class CreateLookupTypeCommandHandler : IRequestHandler<CreateLookupTypeCommand, Result<Guid>>
{
    private readonly ICoreDbContext _context;

    public CreateLookupTypeCommandHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateLookupTypeCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.LookupTypes.AnyAsync(t => t.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' kodu zaten mevcut.");

        var type = new LookupType
        {
            Code = request.Code,
            NameI18n = request.NameI18n,
            Description = request.Description,
            IsSystem = false
        };

        _context.LookupTypes.Add(type);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(type.Id);
    }
}
