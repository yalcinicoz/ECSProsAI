using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpsertLabelTemplate;

/// <summary>K7: şablon oluştur/güncelle. IsDefault=true verilirse aynı hedef tipteki diğer varsayılan kaldırılır.</summary>
public record UpsertLabelTemplateCommand(
    Guid? Id,
    string Name,
    string TargetType,
    decimal WidthMm,
    decimal HeightMm,
    string ElementsJson,
    bool IsDefault,
    bool IsActive) : IRequest<Result<Guid>>;

public class UpsertLabelTemplateCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpsertLabelTemplateCommand, Result<Guid>>
{
    private static readonly string[] Targets = ["product", "bin"];

    public async Task<Result<Guid>> Handle(UpsertLabelTemplateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Result.Failure<Guid>("Şablon adı zorunlu.");
        if (!Targets.Contains(request.TargetType)) return Result.Failure<Guid>("Geçersiz hedef tip (product|bin).");
        if (request.WidthMm is < 10 or > 500 || request.HeightMm is < 10 or > 500)
            return Result.Failure<Guid>("Kağıt ölçüsü 10-500 mm aralığında olmalı.");
        try
        {
            using var doc = JsonDocument.Parse(request.ElementsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Result.Failure<Guid>("Elemanlar listesi geçersiz.");
        }
        catch (JsonException) { return Result.Failure<Guid>("Elemanlar listesi geçersiz JSON."); }

        LabelTemplate? t = null;
        if (request.Id.HasValue)
        {
            t = await db.LabelTemplates.FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct);
            if (t is null) return Result.Failure<Guid>("Şablon bulunamadı.");
        }
        if (t is null)
        {
            var baseCode = string.Join("-", request.Name.Trim().ToLowerInvariant()
                .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
            baseCode = new string(baseCode.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            if (baseCode.Length == 0) baseCode = "etiket";
            if (baseCode.Length > 50) baseCode = baseCode[..50];
            var code = baseCode; var n = 1;
            while (await db.LabelTemplates.AnyAsync(x => x.Code == code, ct)) code = $"{baseCode}-{++n}";
            t = new LabelTemplate { Code = code };
            db.LabelTemplates.Add(t);
        }
        t.Name = request.Name.Trim();
        t.TargetType = request.TargetType;
        t.WidthMm = request.WidthMm;
        t.HeightMm = request.HeightMm;
        t.ElementsJson = request.ElementsJson;
        t.IsActive = request.IsActive;
        t.UpdatedAt = DateTime.UtcNow;

        if (request.IsDefault && !t.IsDefault)
        {
            var others = await db.LabelTemplates
                .Where(x => x.TargetType == request.TargetType && x.IsDefault && x.Id != t.Id).ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
        }
        t.IsDefault = request.IsDefault;

        await db.SaveChangesAsync(ct);
        return Result.Success(t.Id);
    }
}
