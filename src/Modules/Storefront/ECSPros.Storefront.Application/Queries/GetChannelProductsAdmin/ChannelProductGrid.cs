using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Grid;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductsAdmin;

/// <summary>
/// Kanal Ürünleri DataGrid şeması (2026-09-09).
///
/// ★ ÖNEMLİ SINIR: bu ekran İKİ modülü birleştirir — liste tabanı Catalog <c>Products</c>, kanal
/// durumu ise Storefront <c>channel_products</c> satırlarından BELLEKTE çözülür (opt-out semantiği:
/// satır yok ⇒ kanalda). Bu yüzden şema YALNIZ ürün alanlarını kapsar; <c>isSelected</c>,
/// <c>saleStopped*</c>, <c>isStoppedNow</c> ve listeleme durumu sunucuda SIRALANAMAZ/FİLTRELENEMEZ —
/// onlar mevcut adlandırılmış süzgeçlerle (<c>status</c>, <c>listing</c>, <c>reason</c>) çalışır.
/// Kanal durumunu şemaya taşımak, opt-out semantiğini tek sorguda kurmayı gerektirir (satırı olmayan
/// ürün de "kanalda" sayılır) — bugünkü kurguda karşılığı yok, uydurma çözüm ARAMA.
///
/// ★ Bu modül jsonb için <c>PgJsonFunctions.JsonText</c> kullanıyor (Catalog'a ait helper);
/// <c>GridJson.Text</c> ile karıştırma — CatalogDbContext ikisini de tanır ama şema tek birini seçmeli.
/// </summary>
public static class ChannelProductGrid
{
    public static readonly string[] SourceTypes = { "own", "seller", "supply" };

    public static readonly GridSchema<Product> Schema = new GridSchema<Product>()
        .Text("code", p => p.Code)
        .Text("name", p => ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(p.NameI18n, "tr"))
        .Text("supplierProductCode", p => p.SupplierProductCode)
        .Enum("sourceType", p => p.SourceType, SourceTypes)
        .Bool("isSaleOpen", p => p.IsSaleOpen)
        .Number("basePrice", p => p.BasePrice)
        .Number("variantCount", p => p.Variants.Count(v => !v.IsDeleted))
        .Date("createdAt", p => p.CreatedAt)
        .Guid("productGroupId", p => p.ProductGroupId)
        .Guid("supplierId", p => p.SupplierId)
        .Sort("code", p => p.Code)
        .Sort("name", p => ECSPros.Catalog.Application.Helpers.PgJsonFunctions.JsonText(p.NameI18n, "tr"))
        .Sort("sourceType", p => p.SourceType)
        .Sort("isSaleOpen", p => p.IsSaleOpen)
        .Sort("basePrice", p => p.BasePrice)
        .Sort("variantCount", p => p.Variants.Count(v => !v.IsDeleted))
        .Sort("createdAt", p => p.CreatedAt)
        .DefaultSort(p => p.Code, desc: false)
        .TieBreaker(p => p.Id);
}
