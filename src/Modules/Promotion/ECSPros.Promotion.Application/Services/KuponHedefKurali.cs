namespace ECSPros.Promotion.Application.Services;

/// <summary>
/// Kupon hedef (kime tanımlı) kuralı — TEK yer. Kupon üç şekilde hedeflenebilir:
/// herkese açık (MemberId ve MemberGroupId null), belirli bir ÜYEYE özel (MemberId dolu)
/// veya bir ÜYE GRUBUNA açık (MemberGroupId dolu). Alanlar veritabanında baştan vardı
/// ama tanım (create/update) ve doğrulama tarafına bağlı değildi: kişiye özel kupon
/// tanımlanamıyordu ve tanımlansa bile misafir sepetinde (MemberId null) doğrulamadan
/// GEÇİYORDU. Kural buraya alındı ki ValidateCoupon ve UseCoupon aynı cevabı versin.
/// </summary>
public static class KuponHedefKurali
{
    public const string GirisGerekli = "Bu kupon kişiye özeldir; kullanmak için giriş yapmalısınız.";
    public const string BaskasinaAit = "Bu kupon size ait değil.";
    public const string GrupDisi = "Bu kupon yalnız belirli bir üye grubu için geçerlidir.";
    public const string IkisiBirden =
        "Kupon ya belirli bir üyeye ya da bir üye grubuna tanımlanabilir; ikisi birden seçilemez.";

    /// <summary>Kuponu kullanmaya çalışan (belki misafir) için engel metni; null ise engel yok.</summary>
    public static string? Engel(
        Guid? kuponMemberId, Guid? kuponMemberGroupId,
        Guid? istekMemberId, Guid? istekMemberGroupId)
    {
        if (kuponMemberId.HasValue)
        {
            if (!istekMemberId.HasValue) return GirisGerekli;
            return kuponMemberId.Value == istekMemberId.Value ? null : BaskasinaAit;
        }

        if (kuponMemberGroupId.HasValue)
        {
            if (!istekMemberId.HasValue) return GirisGerekli;
            return istekMemberGroupId == kuponMemberGroupId.Value ? null : GrupDisi;
        }

        return null; // herkese açık
    }

    /// <summary>Tanım sırasındaki hedef geçerliliği; null ise sorun yok.</summary>
    public static string? TanimHatasi(Guid? memberId, Guid? memberGroupId)
        => memberId.HasValue && memberGroupId.HasValue ? IkisiBirden : null;

    /// <summary>Kupon grup hedefliyse üyenin grubu gerekir — gereksiz CRM sorgusunu önler.</summary>
    public static bool UyeGrubuGerekli(Guid? kuponMemberGroupId, Guid? istekMemberId)
        => kuponMemberGroupId.HasValue && istekMemberId.HasValue;
}
