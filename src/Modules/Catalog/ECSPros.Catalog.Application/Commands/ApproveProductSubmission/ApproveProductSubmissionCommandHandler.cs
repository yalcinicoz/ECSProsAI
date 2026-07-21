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
        var submission = await _db.ProductSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, ct);
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

        // Aynı tedarikçi + kod için canlı ürün varsa: revizyon (bu dilimde değil).
        var liveExists = await _db.Products.AnyAsync(p =>
            p.SupplierId == submission.SupplierId && p.SupplierProductCode == submission.SupplierProductCode, ct);
        if (liveExists)
            return Result.Failure<ApproveProductSubmissionResult>(
                "Bu tedarikçi ve kod için zaten canlı bir ürün var (revizyon onayı henüz desteklenmiyor).");

        var group = await _db.ProductGroups
            .Include(g => g.Attributes.Where(a => !a.IsDeleted)).ThenInclude(a => a.AttributeType)
            .FirstOrDefaultAsync(g => g.Code == submission.GroupCode && g.IsActive, ct);
        if (group is null)
            return Result.Failure<ApproveProductSubmissionResult>($"Grup bulunamadı: {submission.GroupCode}");

        // Değer çözümleyici: (attributeTypeId, küçük-harf tr ad) → AttributeValue.Id
        var typeIds = group.Attributes.Select(a => a.AttributeTypeId).Distinct().ToList();
        var poolValues = await _db.AttributeValues
            .Where(v => typeIds.Contains(v.AttributeTypeId) && v.IsActive)
            .ToListAsync(ct);
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

        // Benzersiz ürün kodu
        string code;
        do { code = $"PRD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"; }
        while (await _db.Products.AnyAsync(p => p.Code == code, ct));

        var product = new Product
        {
            ProductGroupId = group.Id,
            Code = code,
            NameI18n = body.Name ?? new(),
            ShortDescriptionI18n = body.ShortDescription,
            DescriptionI18n = body.Description,
            SupplierId = submission.SupplierId,
            SupplierProductCode = submission.SupplierProductCode,
            BasePrice = 0,
            TaxRate = 18,
            IsSaleOpen = false   // onay = katalogda oluştur; satışa açma mevcut panel akışıyla
        };

        // Ürün-seviyesi özellikler (havuz değeri → Id)
        foreach (var (attrCode, el) in body.Attributes ?? new())
        {
            if (!prodType.TryGetValue(attrCode, out var typeId)) continue; // Kapı 1 elemişti; savunmacı
            foreach (var valName in ExtractValues(el))
            {
                var vid = Resolve(typeId, valName);
                if (vid is null)
                    return Result.Failure<ApproveProductSubmissionResult>($"'{valName}' değeri artık havuzda yok ({attrCode}).");
                product.Attributes.Add(new ProductAttribute { AttributeTypeId = typeId, AttributeValueId = vid });
            }
        }

        // Varyantlar + eksen değerleri + görseller
        decimal? firstPrice = null;
        foreach (var vb in body.Variants)
        {
            var price = vb.Price?.Amount ?? 0m;
            firstPrice ??= price;
            var variant = new ProductVariant
            {
                Sku = vb.Sku ?? code,
                Barcode = string.IsNullOrWhiteSpace(vb.Barcode) ? null : vb.Barcode,
                BasePrice = price,
                IsActive = true
            };

            foreach (var (axisCode, valName) in vb.AxisValues ?? new())
            {
                if (!axisType.TryGetValue(axisCode, out var typeId)) continue;
                var vid = Resolve(typeId, valName);
                if (vid is null)
                    return Result.Failure<ApproveProductSubmissionResult>($"'{valName}' eksen değeri artık havuzda yok ({axisCode}).");
                variant.VariantAttributes.Add(new ProductVariantAttribute { AttributeTypeId = typeId, AttributeValueId = vid.Value });
            }

            // Tedarikçinin gönderdiği görseller (varyanta bağlı) — URL bazlı; onay sonrası panelden zenginleştirilir.
            var imgs = (body.Images ?? new()).Where(i => i.VariantRef == vb.Sku && !string.IsNullOrWhiteSpace(i.Url)).ToList();
            for (int i = 0; i < imgs.Count; i++)
                variant.Images.Add(new ProductVariantImage { ImageUrl = imgs[i].Url!, IsMain = imgs[i].Main == true, SortOrder = i });

            product.Variants.Add(variant);
        }

        product.BasePrice = firstPrice ?? 0m;

        _db.Products.Add(product);

        submission.Status = "approved";
        submission.ProductId = product.Id;
        submission.ProductCode = product.Code;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedBy = request.ReviewedBy;

        await _db.SaveChangesAsync(ct);

        return Result.Success(new ApproveProductSubmissionResult(product.Id, product.Code));
    }

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
