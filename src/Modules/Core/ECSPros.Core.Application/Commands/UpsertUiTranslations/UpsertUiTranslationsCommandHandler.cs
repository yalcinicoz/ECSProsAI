using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpsertUiTranslations;

public class UpsertUiTranslationsCommandHandler
    : IRequestHandler<UpsertUiTranslationsCommand, Result<int>>
{
    private readonly ICoreDbContext _context;

    public UpsertUiTranslationsCommandHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(
        UpsertUiTranslationsCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return Result.Success(0);

        // Fetch existing records that match namespace+key+lang combinations
        var namespaces = request.Items.Select(i => i.Namespace).Distinct().ToList();
        var existing = await _context.UiTranslations
            .Where(x => namespaces.Contains(x.Namespace))
            .ToListAsync(cancellationToken);

        var existingMap = existing.ToDictionary(
            x => (x.Namespace, x.Key, x.Lang));

        int changed = 0;
        foreach (var item in request.Items)
        {
            var compositeKey = (item.Namespace, item.Key, item.Lang);
            if (existingMap.TryGetValue(compositeKey, out var record))
            {
                if (record.Value != item.Value)
                {
                    record.Value = item.Value;
                    changed++;
                }
            }
            else
            {
                _context.UiTranslations.Add(new UiTranslation
                {
                    Id        = Guid.NewGuid(),
                    Namespace = item.Namespace,
                    Key       = item.Key,
                    Lang      = item.Lang,
                    Value     = item.Value,
                });
                changed++;
            }
        }

        if (changed > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(changed);
    }
}
