using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.UpsertCardMessage;

/// <summary>Ürün Kartı F2: kart mesajı ekle/güncelle (Id null = yeni kayıt).</summary>
public record UpsertCardMessageCommand(
    Guid? Id,
    Guid FirmPlatformId,
    int Slot,
    Dictionary<string, string> MessageI18n,
    string? Icon,
    string? Color,
    string ScopeType,
    List<Guid>? ScopeCategoryIds,
    List<string>? ScopeProductCodes,
    DateTime? StartDate,
    DateTime? EndDate,
    int SortOrder,
    bool IsActive) : IRequest<Result<Guid>>;

public class UpsertCardMessageCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<UpsertCardMessageCommand, Result<Guid>>
{
    private static readonly string[] GecerliRenkler = ["yesil", "turuncu", "bordo", "pembe"];

    public async Task<Result<Guid>> Handle(UpsertCardMessageCommand request, CancellationToken ct)
    {
        if (request.Slot is < 1 or > 3)
            return Result.Failure<Guid>("Alan 1-3 aralığında olmalı.");
        if (request.MessageI18n.Values.All(string.IsNullOrWhiteSpace))
            return Result.Failure<Guid>("Mesaj metni boş olamaz.");
        if (request.ScopeType is not ("all" or "category" or "products"))
            return Result.Failure<Guid>("Kapsam all/category/products olmalı.");
        if (request.ScopeType == "category" && request.ScopeCategoryIds is not { Count: > 0 })
            return Result.Failure<Guid>("Kategori kapsamı için en az bir kategori seçilmeli.");
        if (request.ScopeType == "products" && request.ScopeProductCodes is not { Count: > 0 })
            return Result.Failure<Guid>("Ürün kapsamı için en az bir ürün kodu girilmeli.");
        if (request.Color is { Length: > 0 } && !GecerliRenkler.Contains(request.Color))
            return Result.Failure<Guid>("Renk yesil/turuncu/bordo/pembe olmalı.");
        // İkon CSS sınıfına ve JS innerHTML'e girer — yalnız güvenli karakter seti
        if (request.Icon is { Length: > 0 }
            && !System.Text.RegularExpressions.Regex.IsMatch(request.Icon.Trim(), "^[a-z0-9-]+$"))
            return Result.Failure<Guid>("İkon yalnız küçük harf/rakam/tire içerebilir (örn. fa-truck).");
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
            return Result.Failure<Guid>("Bitiş tarihi başlangıçtan önce olamaz.");

        CardMessage mesaj;
        if (request.Id is { } id)
        {
            var mevcut = await db.CardMessages
                .FirstOrDefaultAsync(m => m.Id == id && m.FirmPlatformId == request.FirmPlatformId, ct);
            if (mevcut is null) return Result.Failure<Guid>("Mesaj bulunamadı.");
            mesaj = mevcut;
        }
        else
        {
            mesaj = new CardMessage { FirmPlatformId = request.FirmPlatformId };
            db.CardMessages.Add(mesaj);
        }

        mesaj.Slot = request.Slot;
        mesaj.MessageI18n = request.MessageI18n;
        mesaj.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        mesaj.Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color;
        mesaj.ScopeType = request.ScopeType;
        mesaj.ScopeCategoryIds = request.ScopeType == "category" ? request.ScopeCategoryIds : null;
        mesaj.ScopeProductCodes = request.ScopeType == "products"
            ? request.ScopeProductCodes!.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList()
            : null;
        mesaj.StartDate = request.StartDate;
        mesaj.EndDate = request.EndDate;
        mesaj.SortOrder = request.SortOrder;
        mesaj.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return Result.Success(mesaj.Id);
    }
}
