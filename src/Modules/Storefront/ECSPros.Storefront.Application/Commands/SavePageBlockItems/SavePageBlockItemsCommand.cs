using System.Text.Json;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SavePageBlockItems;

/// <summary>
/// G6: bloğun öğe listesini toplu kaydeder — SaveNavNodes deseni: mevcutlar soft-delete,
/// liste sıfırdan yazılır (admin editörü tam listeyi gönderir). Öğe kuralı yalnız
/// katalogda öğe seviyesi kural taşıyan tiplerde kabul edilir (spec: banner öğeleri kuralsız).
/// </summary>
public record SavePageBlockItemsCommand(
    Guid BlockId,
    Guid FirmPlatformId,
    List<PageBlockItemInput> Items) : IRequest<Result>;

public record PageBlockItemInput(
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    string? ImageUrl,
    string? MobileImageUrl,
    string? VideoUrl,
    string? LinkUrl,
    bool OpenInNewTab,
    Dictionary<string, string>? ButtonTextI18n,
    string? BadgeLabel,
    int SortOrder,
    bool IsActive,
    DateTime? StartAt,
    DateTime? EndAt,
    int Priority,
    string? RuleJson,
    string? ConfigJson);

public class SavePageBlockItemsCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SavePageBlockItemsCommand, Result>
{
    public async Task<Result> Handle(SavePageBlockItemsCommand request, CancellationToken ct)
    {
        var blok = await db.PageBlocks.FirstOrDefaultAsync(
            b => b.Id == request.BlockId && b.FirmPlatformId == request.FirmPlatformId, ct);
        if (blok is null)
            return Result.Failure("Blok bulunamadı.");

        var ogeKurali = PageBlockCatalog.AllowsItemRules(blok.BlockType);
        foreach (var oge in request.Items)
        {
            if (oge.RuleJson is not null && !ogeKurali)
                return Result.Failure($"'{blok.BlockType}' tipinde öğe kuralı verilemez.");
            if (!GecerliJson(oge.RuleJson) || !GecerliJson(oge.ConfigJson))
                return Result.Failure("Öğe JSON alanı geçersiz.");
        }

        var mevcutlar = await db.PageBlockItems
            .Where(i => i.PageBlockId == request.BlockId)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var mevcut in mevcutlar)
        {
            mevcut.IsDeleted = true;
            mevcut.DeletedAt = now;
        }

        foreach (var oge in request.Items)
        {
            db.PageBlockItems.Add(new PageBlockItem
            {
                PageBlockId = request.BlockId,
                TitleI18n = oge.TitleI18n,
                SubtitleI18n = oge.SubtitleI18n,
                ImageUrl = oge.ImageUrl,
                MobileImageUrl = oge.MobileImageUrl,
                VideoUrl = oge.VideoUrl,
                LinkUrl = oge.LinkUrl,
                OpenInNewTab = oge.OpenInNewTab,
                ButtonTextI18n = oge.ButtonTextI18n,
                BadgeLabel = oge.BadgeLabel,
                SortOrder = oge.SortOrder,
                IsActive = oge.IsActive,
                StartAt = oge.StartAt,
                EndAt = oge.EndAt,
                Priority = oge.Priority,
                RuleJson = oge.RuleJson,
                ConfigJson = oge.ConfigJson,
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static bool GecerliJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try { using var _ = JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }
}
