using System.Text.Json;
using ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.ApproveProductSubmission;

public class ApproveProductSubmissionCommandHandler
    : IRequestHandler<ApproveProductSubmissionCommand, Result<ApproveProductSubmissionResult>>
{
    private readonly ICatalogDbContext _db;

    public ApproveProductSubmissionCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<ApproveProductSubmissionResult>> Handle(ApproveProductSubmissionCommand request, CancellationToken ct)
    {
        var submission = await _db.ProductSubmissions.FirstOrDefaultAsync(s => s.Id == request.SubmissionId, ct);
        if (submission is null)
            return Result.Failure<ApproveProductSubmissionResult>("Gönderim bulunamadı.");
        if (submission.Status != "pending")
            return Result.Failure<ApproveProductSubmissionResult>($"Gönderim '{submission.Status}' durumunda; yalnız pending onaylanır.");

        PartnerProductBody? body;
        try { body = JsonSerializer.Deserialize<PartnerProductBody>(submission.PayloadJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { return Result.Failure<ApproveProductSubmissionResult>("Gönderim gövdesi çözümlenemedi."); }
        if (body is null || body.Variants is null || body.Variants.Count == 0)
            return Result.Failure<ApproveProductSubmissionResult>("Gönderim gövdesi geçersiz.");

        // Mevcut canlı ürün? Varsa REVİZYON (güncelle), yoksa YENİ (oluştur).
        var product = await _db.Products
            .Include(p => p.Variants.Where(v => !v.IsDeleted)).ThenInclude(v => v.VariantAttributes.Where(a => !a.IsDeleted))
            .Include(p => p.Attributes.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(p => p.SupplierId == submission.SupplierId
                && p.SupplierProductCode == submission.SupplierProductCode, ct);
        var isRevision = product is not null;
        if (product is not null && product.SourceType != "seller")
            product.SourceType = "seller";   // F5 K6: eski kayıtlar için idempotent damga

        var group = await _db.ProductGroups
            .Include(g => g.Attributes.Where(a => !a.IsDeleted)).ThenInclude(a => a.AttributeType)
            .FirstOrDefaultAsync(g => g.Code == submission.GroupCode && g.IsActive, ct);
        if (group is null)
            return Result.Failure<ApproveProductSubmissionResult>($"Grup bulunamadı: {submission.GroupCode}");

        // Değer çözümleyici: (attributeTypeId, küçük-harf tr ad) → AttributeValue.Id
        var typeIds = group.Attributes.Select(a => a.AttributeTypeId).Distinct().ToList();
        var poolValues = await _db.AttributeValues
            .Where(v => typeIds.Contains(v.AttributeTypeId) && v.IsActive).ToListAsync(ct);
        var resolver = new Dictionary<(Guid, string), Guid>();
        foreach (var v in poolValues)
        {
            var name = v.NameI18n.TryGetValue("tr", out var tr) ? tr : v.NameI18n.Values.FirstOrDefault() ?? "";
            if (name.Length > 0) resolver[(v.AttributeTypeId, name.ToLowerInvariant())] = v.Id;
        }
        Guid? Resolve(Guid typeId, string? name) =>
            !string.IsNullOrWhiteSpace(name) && resolver.TryGetValue((typeId, name.Trim().ToLowerInvariant()), out var id) ? id : null;

        var axisType = group.Attributes.Where(a => a.IsVariant).ToDictionary(a => a.AttributeType.Code, a => a.AttributeTypeId);
        var prodType = group.Attributes.Where(a => !a.IsVariant).ToDictionary(a => a.AttributeType.Code, a => a.AttributeTypeId);

        // ── Önce TÜMÜNÜ çöz (havuz hatası varsa hiç mutasyon yapmadan dön) ──
        var resolvedAttrs = new List<(Guid TypeId, Guid ValueId)>();
        foreach (var (attrCode, el) in body.Attributes ?? new())
        {
            if (!prodType.TryGetValue(attrCode, out var typeId)) continue; // Kapı 1 elemişti; savunmacı
            foreach (var valName in ExtractValues(el))
            {
                var vid = Resolve(typeId, valName);
                if (vid is null)
                    return Result.Failure<ApproveProductSubmissionResult>($"'{valName}' değeri artık havuzda yok ({attrCode}).");
                resolvedAttrs.Add((typeId, vid.Value));
            }
        }

        var resolvedVariants = new List<ResolvedVariant>();
        foreach (var vb in body.Variants)
        {
            var axis = new List<(Guid, Guid)>();
            foreach (var (axisCode, valName) in vb.AxisValues ?? new())
            {
                if (!axisType.TryGetValue(axisCode, out var typeId)) continue;
                var vid = Resolve(typeId, valName);
                if (vid is null)
                    return Result.Failure<ApproveProductSubmissionResult>($"'{valName}' eksen değeri artık havuzda yok ({axisCode}).");
                axis.Add((typeId, vid.Value));
            }
            var imgs = (body.Images ?? new())
                .Where(i => i.VariantRef == vb.Sku && !string.IsNullOrWhiteSpace(i.Url))
                .Select(i => (Url: i.Url!, Main: i.Main == true)).ToList();
            resolvedVariants.Add(new ResolvedVariant(
                vb.Sku ?? "", string.IsNullOrWhiteSpace(vb.Barcode) ? null : vb.Barcode,
                vb.Price?.Amount ?? 0m, axis, imgs));
        }
        var firstPrice = resolvedVariants.Count > 0 ? resolvedVariants[0].Price : 0m;

        if (!isRevision)
        {
            // ── YENİ ürün — tüm grafik yeni (koleksiyon-ekle güvenli) ──
            string code;
            do { code = $"PRD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"; }
            while (await _db.Products.AnyAsync(p => p.Code == code, ct));

            product = new Product
            {
                ProductGroupId = group.Id, Code = code,
                NameI18n = body.Name ?? new(), ShortDescriptionI18n = body.ShortDescription, DescriptionI18n = body.Description,
                SupplierId = submission.SupplierId,
                SourceType = "seller",   // F5 K6: satıcı kaynaklı ürün
                SupplierProductCode = submission.SupplierProductCode,
                BasePrice = firstPrice, TaxRate = 18, IsSaleOpen = false
            };
            foreach (var (tid, vid) in resolvedAttrs)
                product.Attributes.Add(new ProductAttribute { AttributeTypeId = tid, AttributeValueId = vid });
            foreach (var rv in resolvedVariants)
                product.Variants.Add(BuildVariant(rv));
            _db.Products.Add(product);
        }
        else
        {
            // ── REVİZYON — canlı ürünü güncelle (tracked parent → çocukları DbSet.Add ile) ──
            var payloadSkus = resolvedVariants.Select(r => r.Sku).ToList();
            var conflict = await _db.ProductVariants
                .Where(v => payloadSkus.Contains(v.Sku) && v.ProductId != product!.Id)
                .Select(v => v.Sku).FirstOrDefaultAsync(ct);
            if (conflict is not null)
                return Result.Failure<ApproveProductSubmissionResult>($"SKU başka bir üründe kullanımda: {conflict}");

            product!.NameI18n = body.Name ?? product.NameI18n;
            product.ShortDescriptionI18n = body.ShortDescription;
            product.DescriptionI18n = body.Description;
            product.BasePrice = firstPrice;

            // Ürün özellikleri: hard-delete + yeniden ekle (unique index soft-delete'i kapsıyor)
            _db.ProductAttributes.RemoveRange(product.Attributes);
            foreach (var (tid, vid) in resolvedAttrs)
                _db.ProductAttributes.Add(new ProductAttribute { ProductId = product.Id, AttributeTypeId = tid, AttributeValueId = vid });

            // Varyant senkronu (sku ile): mevcut güncelle · yeni ekle · eksik pasifleştir (silme — sipariş referansı)
            var existingBySku = product.Variants.ToDictionary(v => v.Sku);
            var seen = new HashSet<string>();
            foreach (var rv in resolvedVariants)
            {
                seen.Add(rv.Sku);
                if (existingBySku.TryGetValue(rv.Sku, out var ev))
                {
                    ev.BasePrice = rv.Price; ev.Barcode = rv.Barcode; ev.IsActive = true;
                    _db.ProductVariantAttributes.RemoveRange(ev.VariantAttributes);
                    foreach (var (tid, vid) in rv.Axis)
                        _db.ProductVariantAttributes.Add(new ProductVariantAttribute { VariantId = ev.Id, AttributeTypeId = tid, AttributeValueId = vid });
                }
                else
                {
                    var nv = BuildVariant(rv);
                    nv.ProductId = product.Id;
                    _db.ProductVariants.Add(nv);
                }
            }
            foreach (var ev in product.Variants.Where(v => !seen.Contains(v.Sku)))
                ev.IsActive = false;
        }

        submission.Status = "approved";
        submission.ProductId = product!.Id;
        submission.ProductCode = product.Code;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedBy = request.ReviewedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(new ApproveProductSubmissionResult(product.Id, product.Code));
    }

    // Yeni (untracked) varyant grafiği — koleksiyon-ekle güvenli çünkü parent henüz tracked değil.
    private static ProductVariant BuildVariant(ResolvedVariant rv)
    {
        var variant = new ProductVariant { Sku = rv.Sku, Barcode = rv.Barcode, BasePrice = rv.Price, IsActive = true };
        foreach (var (tid, vid) in rv.Axis)
            variant.VariantAttributes.Add(new ProductVariantAttribute { AttributeTypeId = tid, AttributeValueId = vid });
        for (int i = 0; i < rv.Images.Count; i++)
            variant.Images.Add(new ProductVariantImage { ImageUrl = rv.Images[i].Url, IsMain = rv.Images[i].Main, SortOrder = i });
        return variant;
    }

    private sealed record ResolvedVariant(
        string Sku, string? Barcode, decimal Price,
        List<(Guid TypeId, Guid ValueId)> Axis, List<(string Url, bool Main)> Images);

    private static List<string> ExtractValues(JsonElement el)
    {
        var list = new List<string>();
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
                }
        }
        return list;
    }
}
