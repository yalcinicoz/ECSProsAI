using System.Text.Json;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SavePageBlock;

/// <summary>
/// G6: taslak blok upsert'i (Id yoksa oluşturur). Doğrulama PageBlockCatalog'dan —
/// Yayınla'daki kurallarla aynı kaynak: admin geçersiz bloğu taslağa bile yazamaz
/// (yayında ikinci kez denetlenir; savunma iki katmanlı). Yalnız taslak tablo yazılır;
/// canlı yayına etkisi Yayınla'ya kadar YOKtur.
/// </summary>
public record SavePageBlockCommand(
    Guid? Id,
    Guid FirmPlatformId,
    string Placement,
    string BlockType,
    string? Template,
    Dictionary<string, string> TitleI18n,
    Dictionary<string, string>? SubtitleI18n,
    int SortOrder,
    bool IsActive,
    DateTime? StartAt,
    DateTime? EndAt,
    int Priority,
    string? RuleJson,
    string? ConfigJson) : IRequest<Result<Guid>>;

public class SavePageBlockCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SavePageBlockCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SavePageBlockCommand request, CancellationToken ct)
    {
        var def = PageBlockCatalog.Find(request.BlockType);
        if (def is null)
            return Result.Failure<Guid>($"Bilinmeyen blok tipi: {request.BlockType}");
        if (!PageBlockCatalog.IsValidPlacement(request.Placement))
            return Result.Failure<Guid>($"Geçersiz yerleşim: {request.Placement}");
        if (!PageBlockCatalog.IsValidTemplate(request.BlockType, request.Template))
            return Result.Failure<Guid>($"'{request.BlockType}' için geçersiz şablon: {request.Template ?? "(boş)"}");
        if (request.RuleJson is not null && !PageBlockCatalog.AllowsBlockRules(request.BlockType))
            return Result.Failure<Guid>("Bu blok tipinde kural öğe seviyesindedir; blok kuralı verilemez.");
        if (!GecerliJson(request.RuleJson))
            return Result.Failure<Guid>("Kural JSON'ı geçersiz.");
        // G10: kural şeması (alan adları/değer kümeleri) — Yayınla'yla aynı katalog kaynağı
        if (PageBlockCatalog.ValidateRule(request.RuleJson) is { Count: > 0 } kuralHatalari)
            return Result.Failure<Guid>("Kural geçersiz: " + string.Join(" | ", kuralHatalari));
        if (!GecerliJson(request.ConfigJson))
            return Result.Failure<Guid>("Config JSON'ı geçersiz.");
        if (request.StartAt is not null && request.EndAt is not null && request.EndAt < request.StartAt)
            return Result.Failure<Guid>("Bitiş tarihi başlangıçtan önce olamaz.");

        PageBlock blok;
        if (request.Id is { } id)
        {
            var mevcut = await db.PageBlocks.FirstOrDefaultAsync(
                b => b.Id == id && b.FirmPlatformId == request.FirmPlatformId, ct);
            if (mevcut is null)
                return Result.Failure<Guid>("Blok bulunamadı.");
            blok = mevcut;
        }
        else
        {
            blok = new PageBlock { FirmPlatformId = request.FirmPlatformId };
            db.PageBlocks.Add(blok);
        }

        blok.Placement = request.Placement;
        blok.BlockType = request.BlockType;
        blok.Template = string.IsNullOrEmpty(request.Template) ? null : request.Template;
        blok.TitleI18n = request.TitleI18n;
        blok.SubtitleI18n = request.SubtitleI18n;
        blok.SortOrder = request.SortOrder;
        blok.IsActive = request.IsActive;
        blok.StartAt = request.StartAt;
        blok.EndAt = request.EndAt;
        blok.Priority = request.Priority;
        blok.RuleJson = request.RuleJson;
        blok.ConfigJson = request.ConfigJson;

        await db.SaveChangesAsync(ct);
        return Result.Success(blok.Id);
    }

    private static bool GecerliJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try { using var _ = JsonDocument.Parse(json); return true; }
        catch (JsonException) { return false; }
    }
}
