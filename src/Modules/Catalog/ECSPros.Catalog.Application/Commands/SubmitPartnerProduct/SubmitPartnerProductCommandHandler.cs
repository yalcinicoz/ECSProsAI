using System.Text.Json;
using System.Text.RegularExpressions;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SubmitPartnerProduct;

public class SubmitPartnerProductCommandHandler
    : IRequestHandler<SubmitPartnerProductCommand, Result<SubmitPartnerProductResult>>
{
    // İçerik kuralı sınırları (§3.8 — başlangıç değerleri, ileride yapılandırılabilir).
    private const int NameMin = 3, NameMax = 150, ShortDescMax = 300, DescMax = 5000;
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex UrlOrEmail = new(@"(https?://|www\.|\S+@\S+\.\S)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRun = new(@"\d{7,}", RegexOptions.Compiled);

    private readonly ICatalogDbContext _db;

    public SubmitPartnerProductCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<SubmitPartnerProductResult>> Handle(SubmitPartnerProductCommand request, CancellationToken ct)
    {
        var body = request.Body;
        var errors = new List<PartnerValidationError>();

        // ── Yapısal: kod + grup ────────────────────────────────────────
        var code = body.SupplierProductCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            errors.Add(new("supplierProductCode", "required", "supplierProductCode zorunludur."));
        else if (code.Length > 100)
            errors.Add(new("supplierProductCode", "too_long", "supplierProductCode en fazla 100 karakter."));

        if (string.IsNullOrWhiteSpace(body.Group))
            errors.Add(new("group", "required", "group zorunludur."));

        // Grup + eksen/özellik + değer havuzu
        var group = string.IsNullOrWhiteSpace(body.Group) ? null : await _db.ProductGroups
            .Include(g => g.Attributes.Where(a => !a.IsDeleted)).ThenInclude(a => a.AttributeType)
            .FirstOrDefaultAsync(g => g.Code == body.Group && g.IsActive, ct);

        if (!string.IsNullOrWhiteSpace(body.Group) && group is null)
            errors.Add(new("group", "not_found", $"Grup bulunamadı veya pasif: {body.Group}"));

        // ── İçerik kuralları (ad/açıklama) ─────────────────────────────
        ValidateName(body.Name, errors);
        ValidateFreeTextLength(body.ShortDescription, "shortDescription", ShortDescMax, errors);
        ValidateFreeTextLength(body.Description, "description", DescMax, errors);
        ValidateForbidden(body.Name, "name", errors);
        ValidateForbidden(body.ShortDescription, "shortDescription", errors);
        ValidateForbidden(body.Description, "description", errors);

        // Grup varsa: havuz değerlerini ve eksen/özellik uyumunu doğrula
        if (group is not null)
        {
            // attribute type code → (allowed value adları [lower], isVariant, isRequired)
            var typeIds = group.Attributes.Select(a => a.AttributeTypeId).Distinct().ToList();
            var values = await _db.AttributeValues
                .Where(v => typeIds.Contains(v.AttributeTypeId) && v.IsActive)
                .ToListAsync(ct);
            var allowedByType = values
                .GroupBy(v => v.AttributeTypeId)
                .ToDictionary(g => g.Key, g => g
                    .Select(v => (v.NameI18n.TryGetValue("tr", out var tr) ? tr : v.NameI18n.Values.FirstOrDefault() ?? ""))
                    .Where(s => s.Length > 0)
                    .Select(s => s.ToLowerInvariant())
                    .ToHashSet());

            var axisAttrs = group.Attributes.Where(a => a.IsVariant)
                .ToDictionary(a => a.AttributeType.Code, a => a);
            var prodAttrs = group.Attributes.Where(a => !a.IsVariant)
                .ToDictionary(a => a.AttributeType.Code, a => a);

            ValidateProductAttributes(body.Attributes, prodAttrs, allowedByType, errors);
            ValidateVariants(body.Variants, axisAttrs, allowedByType, request.CanSetPrice, errors);
        }
        else if (body.Variants is null || body.Variants.Count == 0)
        {
            errors.Add(new("variants", "required", "En az bir varyant gereklidir."));
        }

        if (errors.Count > 0)
            return Result.Success(new SubmitPartnerProductResult(false, null, code, null, errors));

        // ── Kapı 1 geçti → pending submission upsert ───────────────────
        var existing = await _db.ProductSubmissions
            .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId
                && s.SupplierProductCode == code && s.Status == "pending", ct);

        if (existing is null)
        {
            existing = new ProductSubmission
            {
                SupplierId = request.SupplierId,
                SupplierProductCode = code!,
                Status = "pending"
            };
            _db.ProductSubmissions.Add(existing);
        }

        existing.GroupCode = body.Group!;
        existing.Name = body.Name ?? new();
        existing.PayloadJson = request.RawJson;
        existing.VariantCount = body.Variants!.Count;
        existing.ApiClientId = request.ApiClientId;

        await _db.SaveChangesAsync(ct);

        return Result.Success(new SubmitPartnerProductResult(true, existing.Id, code, "pending", new()));
    }

    // ── Doğrulama yardımcıları ─────────────────────────────────────────

    private static void ValidateName(Dictionary<string, string>? name, List<PartnerValidationError> errors)
    {
        if (name is null || !name.TryGetValue("tr", out var tr) || string.IsNullOrWhiteSpace(tr))
        {
            errors.Add(new("name.tr", "required", "Ürün adı (tr) zorunludur."));
            return;
        }
        var len = tr.Trim().Length;
        if (len < NameMin || len > NameMax)
            errors.Add(new("name.tr", "length", $"Ürün adı (tr) {NameMin}-{NameMax} karakter olmalı."));
    }

    private static void ValidateFreeTextLength(Dictionary<string, string>? field, string name, int max, List<PartnerValidationError> errors)
    {
        if (field is null) return;
        foreach (var (lang, val) in field)
            if (val?.Length > max)
                errors.Add(new($"{name}.{lang}", "too_long", $"{name} ({lang}) en fazla {max} karakter."));
    }

    private static void ValidateForbidden(Dictionary<string, string>? field, string name, List<PartnerValidationError> errors)
    {
        if (field is null) return;
        foreach (var (lang, val) in field)
        {
            if (string.IsNullOrEmpty(val)) continue;
            if (HtmlTag.IsMatch(val))
                errors.Add(new($"{name}.{lang}", "html_not_allowed", $"{name} ({lang}) HTML/etiket içeremez."));
            if (UrlOrEmail.IsMatch(val))
                errors.Add(new($"{name}.{lang}", "contact_not_allowed", $"{name} ({lang}) URL/e-posta içeremez."));
            if (PhoneRun.IsMatch(val))
                errors.Add(new($"{name}.{lang}", "contact_not_allowed", $"{name} ({lang}) telefon/uzun rakam dizisi içeremez."));
        }
    }

    private static void ValidateProductAttributes(
        Dictionary<string, JsonElement>? attributes,
        Dictionary<string, Domain.Entities.ProductGroupAttribute> prodAttrs,
        Dictionary<Guid, HashSet<string>> allowedByType,
        List<PartnerValidationError> errors)
    {
        attributes ??= new();

        foreach (var (attrCode, el) in attributes)
        {
            if (!prodAttrs.TryGetValue(attrCode, out var ga))
            {
                errors.Add(new($"attributes.{attrCode}", "unknown_attribute", $"Bu grupta '{attrCode}' özelliği yok."));
                continue;
            }
            var allowed = allowedByType.TryGetValue(ga.AttributeTypeId, out var set) ? set : new HashSet<string>();
            foreach (var v in ExtractValues(el))
                if (!allowed.Contains(v.ToLowerInvariant()))
                    errors.Add(new($"attributes.{attrCode}", "value_not_in_pool", $"'{v}' değeri '{attrCode}' havuzunda yok."));
        }

        // Zorunlu ürün-seviyesi özellikler
        foreach (var (attrCode, ga) in prodAttrs)
            if (ga.IsRequired && (!attributes.ContainsKey(attrCode) || ExtractValues(attributes[attrCode]).Count == 0))
                errors.Add(new($"attributes.{attrCode}", "required", $"'{attrCode}' özelliği zorunludur."));
    }

    private static void ValidateVariants(
        List<PartnerVariantBody>? variants,
        Dictionary<string, Domain.Entities.ProductGroupAttribute> axisAttrs,
        Dictionary<Guid, HashSet<string>> allowedByType,
        bool canSetPrice,
        List<PartnerValidationError> errors)
    {
        if (variants is null || variants.Count == 0)
        {
            errors.Add(new("variants", "required", "En az bir varyant gereklidir."));
            return;
        }

        var axisCodes = axisAttrs.Keys.ToHashSet();

        for (int i = 0; i < variants.Count; i++)
        {
            var v = variants[i];
            var prefix = $"variants[{i}]";

            if (string.IsNullOrWhiteSpace(v.Sku))
                errors.Add(new($"{prefix}.sku", "required", "Varyant sku zorunludur."));

            var axisValues = v.AxisValues ?? new();
            var sentAxes = axisValues.Keys.ToHashSet();

            foreach (var missing in axisCodes.Except(sentAxes))
                errors.Add(new($"{prefix}.axisValues.{missing}", "required", $"Eksen değeri eksik: {missing}."));
            foreach (var extra in sentAxes.Except(axisCodes))
                errors.Add(new($"{prefix}.axisValues.{extra}", "unknown_axis", $"Bu grupta '{extra}' ekseni yok."));

            foreach (var (axisCode, val) in axisValues)
            {
                if (!axisAttrs.TryGetValue(axisCode, out var ga)) continue; // yukarıda raporlandı
                var allowed = allowedByType.TryGetValue(ga.AttributeTypeId, out var set) ? set : new HashSet<string>();
                if (string.IsNullOrWhiteSpace(val) || !allowed.Contains(val.ToLowerInvariant()))
                    errors.Add(new($"{prefix}.axisValues.{axisCode}", "value_not_in_pool", $"'{val}' değeri '{axisCode}' havuzunda yok."));
            }

            if (canSetPrice && (v.Price is null || v.Price.Amount is null or <= 0))
                errors.Add(new($"{prefix}.price", "required", "Pazaryeri tedarikçisi için varyant fiyatı zorunludur."));
        }
    }

    /// <summary>attributes değeri string VEYA dizi olabilir → string listesine indirger.</summary>
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
