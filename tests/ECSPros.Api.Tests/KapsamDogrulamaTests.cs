using ECSPros.Api.Hubs;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y3 3. tur: SignalR abonelik gruplarının kanal ayrımı.
/// Regresyon: önceden yetkili HER kullanıcı tek bir "topic:orders" grubuna giriyor ve tüm
/// kanalların sipariş olaylarını alıyordu; liste filtrelense bile olay bilgisi sızıyordu.
/// </summary>
[TestClass]
public sealed class SignalRKanalGruplariTests
{
    [TestMethod]
    public void Kanal_gruplari_topic_ve_kanala_gore_ayrisir()
    {
        var kanalA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var kanalB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var a = NotificationHub.KanalGrubu("orders", kanalA);
        var b = NotificationHub.KanalGrubu("orders", kanalB);
        var soruA = NotificationHub.KanalGrubu("questions", kanalA);

        Assert.AreNotEqual(a, b, "Farklı kanallar farklı gruplara düşmeli.");
        Assert.AreNotEqual(a, soruA, "Aynı kanal farklı topic'lerde ayrı gruplarda olmalı.");
        Assert.AreNotEqual(a, NotificationHub.TumKanallarGrubu("orders"));
    }

    [TestMethod]
    public void Tum_kanallar_grubu_topic_basinadir()
    {
        Assert.AreEqual("topic:orders:all", NotificationHub.TumKanallarGrubu("orders"));
        Assert.AreEqual("topic:questions:all", NotificationHub.TumKanallarGrubu("questions"));
        Assert.AreNotEqual(NotificationHub.TumKanallarGrubu("orders"),
                           NotificationHub.TumKanallarGrubu("questions"));
    }
}
