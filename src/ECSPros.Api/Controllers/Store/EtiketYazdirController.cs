using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// T3 (K7): kullanıcı tasarımlı şablonla etiket basımı — /yazdir/etiket (ürün) ve /yazdir/etiket-birim (raf).
/// YazdirController kalıbı: GUID bilinmeden erişilemez; yeni sekme JWT taşıyamadığından [AllowAnonymous].
/// Barkod istemci tarafında JsBarcode ile çizilir (13 hane sayısal → EAN13, değilse CODE128).
/// </summary>
[Route("yazdir")]
[AllowAnonymous]
public sealed class EtiketYazdirController(NpgsqlDataSource dataSource) : Controller
{
    private sealed record Element(string Type, string? Field, string? Text,
        double X, double Y, double W, double H, double FontPt, string? Align, bool Bold);

    private sealed record Tpl(decimal WidthMm, decimal HeightMm, List<Element> Elements);

    /// <summary>Tek ürün: variantId&count. Toplu deste (T4 revizyonu): items=vid:adet,vid:adet — her yığının
    /// destesi art arda basılır; etiket basımı SAYIM ÜRETMEZ (keyfi işlem, sayım depoya teslim okutmasıdır).</summary>
    [HttpGet("etiket")]
    public async Task<IActionResult> Etiket(
        [FromQuery] Guid templateId, [FromQuery] Guid? variantId, [FromQuery] int count = 1,
        [FromQuery] string? items = null, CancellationToken ct = default)
    {
        var tpl = await LoadTemplateAsync(templateId, "product", ct);
        if (tpl is null) return NotFound("Şablon bulunamadı.");

        var istek = new List<(Guid Vid, int Count)>();
        if (!string.IsNullOrWhiteSpace(items))
        {
            foreach (var part in items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && Guid.TryParse(kv[0], out var vid) && int.TryParse(kv[1], out var n))
                    istek.Add((vid, Math.Clamp(n, 1, 500)));
            }
        }
        else if (variantId.HasValue) istek.Add((variantId.Value, Math.Clamp(count, 1, 500)));
        if (istek.Count == 0) return NotFound("Basılacak ürün yok.");
        if (istek.Count > 50) return BadRequest("Tek basımda en çok 50 farklı ürün.");
        if (istek.Sum(x => x.Count) > 2000) return BadRequest("Tek basımda en çok 2000 etiket.");

        var desteler = new List<(Dictionary<string, string> Data, int Count)>();
        foreach (var (vid, n) in istek)
        {
            var data = await LoadVariantDataAsync(vid, ct);
            if (data is null) return NotFound($"Varyant bulunamadı: {vid}");
            desteler.Add((data, n));
        }
        return Content(RenderHtmlMulti(tpl, desteler), "text/html", Encoding.UTF8);
    }

    private async Task<Dictionary<string, string>?> LoadVariantDataAsync(Guid variantId, CancellationToken ct)
    {

        // Varyant + ürün + renk/beden değerleri (tek raw-SQL; attribute tipleri definition şemasında)
        const string sql = @"
            SELECT v.""Sku"", COALESCE(v.""Barcode"",''), COALESCE(NULLIF(v.""BasePrice"",0), p.""BasePrice"", 0),
                   COALESCE(p.""NameI18n""->>'tr', p.""Code""), p.""Code"",
                   COALESCE((SELECT av.""NameI18n""->>'tr' FROM catalog.product_variant_attributes va
                             JOIN definition.attribute_types at ON at.""Id"" = va.""AttributeTypeId"" AND at.""Code"" = 'renk'
                             JOIN definition.attribute_values av ON av.""Id"" = va.""AttributeValueId""
                             WHERE va.""VariantId"" = v.""Id"" AND NOT va.""IsDeleted"" LIMIT 1), ''),
                   COALESCE((SELECT av.""NameI18n""->>'tr' FROM catalog.product_variant_attributes va
                             JOIN definition.attribute_types at ON at.""Id"" = va.""AttributeTypeId"" AND at.""Code"" = 'beden'
                             JOIN definition.attribute_values av ON av.""Id"" = va.""AttributeValueId""
                             WHERE va.""VariantId"" = v.""Id"" AND NOT va.""IsDeleted"" LIMIT 1), '')
            FROM catalog.product_variants v
            JOIN catalog.products p ON p.""Id"" = v.""ProductId""
            WHERE v.""Id"" = @id AND NOT v.""IsDeleted""";

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", variantId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;

        var price = r.GetDecimal(2);
        return new Dictionary<string, string>
        {
            ["sku"] = r.GetString(0),
            ["barcode"] = r.GetString(1),
            ["price"] = price.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " ₺",
            ["name"] = r.GetString(3),
            ["code"] = r.GetString(4),
            ["color"] = r.GetString(5),
            ["size"] = r.GetString(6),
        };
    }

    [HttpGet("etiket-birim")]
    public async Task<IActionResult> EtiketBirim(
        [FromQuery] Guid templateId, [FromQuery] Guid binId, [FromQuery] int count = 1, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 100);
        var tpl = await LoadTemplateAsync(templateId, "bin", ct);
        if (tpl is null) return NotFound("Şablon bulunamadı.");

        const string sql = @"
            SELECT b.""Code"", COALESCE(b.""Barcode"",''), COALESCE(s.""NameI18n""->>'tr', s.""Code"", ''),
                   COALESCE(w.""NameI18n""->>'tr', w.""Code"", '')
            FROM inventory.inv_warehouse_bins b
            JOIN inventory.inv_warehouse_sections s ON s.""Id"" = b.""SectionId""
            JOIN inventory.inv_warehouses w ON w.""Id"" = s.""WarehouseId""
            WHERE b.""Id"" = @id AND NOT b.""IsDeleted""";
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", binId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return NotFound("Birim bulunamadı.");

        var data = new Dictionary<string, string>
        {
            ["code"] = r.GetString(0),
            ["barcode"] = string.IsNullOrEmpty(r.GetString(1)) ? r.GetString(0) : r.GetString(1),
            ["section"] = r.GetString(2),
            ["warehouse"] = r.GetString(3),
            ["name"] = r.GetString(2),
        };
        return Content(RenderHtml(tpl, data, count), "text/html", Encoding.UTF8);
    }

    private async Task<Tpl?> LoadTemplateAsync(Guid id, string targetType, CancellationToken ct)
    {
        const string sql = @"SELECT ""WidthMm"", ""HeightMm"", elements::text FROM core.core_label_templates
                             WHERE ""Id"" = @id AND ""TargetType"" = @t AND NOT ""IsDeleted"" AND ""IsActive""";
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("t", targetType);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var elements = JsonSerializer.Deserialize<List<Element>>(r.GetString(2),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        return new Tpl(r.GetDecimal(0), r.GetDecimal(1), elements);
    }

    private static string H(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string RenderHtml(Tpl tpl, Dictionary<string, string> data, int count)
        => RenderHtmlMulti(tpl, new List<(Dictionary<string, string>, int)> { (data, count) });

    private static string RenderHtmlMulti(Tpl tpl, List<(Dictionary<string, string> Data, int Count)> desteler)
    {
        var count = desteler.Sum(d => d.Count);
        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html><html lang=""tr""><head><meta charset=""utf-8""><title>Etiket</title>
<style>
  @page {{ size: {tpl.WidthMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm {tpl.HeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm; margin: 0; }}
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  body {{ font-family: Arial, sans-serif; }}
  .lbl {{ position: relative; width: {tpl.WidthMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm; height: {tpl.HeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm;
          overflow: hidden; page-break-after: always; }}
  .el {{ position: absolute; overflow: hidden; line-height: 1.15; }}
  .bar {{ text-align: center; padding: 4px 8px; background: #f3f3f3; font: 13px Arial; }}
  @media print {{ .bar {{ display: none; }} }}
</style>
<script src=""/js/jsbarcode.min.js""></script></head><body>
<div class=""bar"">Yazdırmak için <button onclick=""window.print()"">Yazdır</button> — {count} etiket</div>");

        foreach (var (data, n) in desteler)
        {
            var one = BuildLabel(tpl, data);
            for (var i = 0; i < n; i++) sb.Append(one);
        }

        sb.Append(@"<script>
document.querySelectorAll('svg.bc').forEach(function(el){
  var v = el.getAttribute('data-value') || '';
  if (!v) return;
  var fmt = /^\d{13}$/.test(v) ? 'EAN13' : 'CODE128';
  try { JsBarcode(el, v, { format: fmt, displayValue: true, margin: 0,
    width: 2, height: Math.max(20, el.parentElement.clientHeight - 18), fontSize: 11 }); }
  catch (e) { try { JsBarcode(el, v, { format: 'CODE128', displayValue: true, margin: 0 }); } catch (e2) {} }
  el.removeAttribute('width'); el.removeAttribute('height');
  el.style.width = '100%'; el.style.height = '100%';
});
</script></body></html>");
        return sb.ToString();
    }

    private static string BuildLabel(Tpl tpl, Dictionary<string, string> data)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder(@"<div class=""lbl"">");
        foreach (var e in tpl.Elements)
        {
            var style = $"left:{e.X.ToString(inv)}mm;top:{e.Y.ToString(inv)}mm;width:{e.W.ToString(inv)}mm;height:{e.H.ToString(inv)}mm;" +
                        $"font-size:{e.FontPt.ToString(inv)}pt;text-align:{(e.Align is "center" or "right" ? e.Align : "left")};" +
                        (e.Bold ? "font-weight:bold;" : "");
            var value = e.Type switch
            {
                "text" => e.Text ?? "",
                "price" => data.GetValueOrDefault("price", ""),
                _ => data.GetValueOrDefault(e.Field ?? "", ""),
            };
            if (e.Type == "barcode")
                sb.Append($@"<div class=""el"" style=""{style}""><svg class=""bc"" data-value=""{H(value)}""></svg></div>");
            else
                sb.Append($@"<div class=""el"" style=""{style}"">{H(value)}</div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }
}
