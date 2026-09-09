using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Y6: isteği yapan kullanıcının HASSAS ALAN izinleri (K6 — beş alan).
/// Süper admin tümünü görür. Yetki kanal kapsamsızdır: alan görünürlüğü kanala göre değişmez.
/// </summary>
public interface IAlanYetkileri
{
    Task<AlanIzinleri> IzinlerAsync(CancellationToken ct = default);
}

public sealed class AlanYetkileri(IHttpContextAccessor http, IEtkinYetkiServisi yetkiServisi) : IAlanYetkileri
{
    public async Task<AlanIzinleri> IzinlerAsync(CancellationToken ct = default)
    {
        var user = http.HttpContext?.User;
        if (user is null) return AlanIzinleri.Yok;
        if (user.FindFirst("sa")?.Value == "true") return AlanIzinleri.Tam;

        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return AlanIzinleri.Yok;

        var y = await yetkiServisi.GetirAsync(userId, ct);
        if (y.SuperAdmin) return AlanIzinleri.Tam;

        return new AlanIzinleri(
            Maliyet: y.Var(Permissions.FieldViewCost),
            Kar:     y.Var(Permissions.FieldViewMargin),
            Telefon: y.Var(Permissions.FieldViewCustomerPhone),
            Adres:   y.Var(Permissions.FieldViewCustomerAddress),
            Notlar:  y.Var(Permissions.FieldViewInternalNotes));
    }
}
