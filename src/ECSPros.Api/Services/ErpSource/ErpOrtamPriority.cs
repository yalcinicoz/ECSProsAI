namespace ECSPros.Api.Services.ErpSource;

/// <summary>V3 tip 8 / kod 1 işareti, tekli Ortam alanında tip 55'in önüne geçer.</summary>
public static class ErpOrtamPriority
{
    public static IReadOnlyList<ErpProductAttributeRow> Apply(
        IReadOnlyList<ErpProductAttributeRow> attributes, bool isTesettur)
    {
        if (!isTesettur) return attributes;
        // Bu değer tip 55'in gerçek kaynak kodu değildir. Sadece kullanıcının mevcut
        // TESETTÜR seçeneği kullanılır; yeni tanım veya sahte kaynak metadata üretilmez.
        return attributes.Where(x => x.KeywordId != "55")
            .Append(new ErpProductAttributeRow("55", "TESETTÜR", "Ortam", null)
                { UseExistingDefinitionOnly = true }).ToArray();
    }
}
