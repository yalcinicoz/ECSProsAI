using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.CreateFilterColor;

public record CreateFilterColorCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string? HexCode,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateFilterColorCommandHandler : IRequestHandler<CreateFilterColorCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _db;

    public CreateFilterColorCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateFilterColorCommand request, CancellationToken cancellationToken)
    {
        var exists = await _db.FilterColors.AnyAsync(c => c.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"Filter color with code '{request.Code}' already exists.");

        var color = new FilterColor
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameI18n = request.NameI18n,
            HexCode = request.HexCode,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _db.FilterColors.Add(color);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(color.Id);
    }
}
