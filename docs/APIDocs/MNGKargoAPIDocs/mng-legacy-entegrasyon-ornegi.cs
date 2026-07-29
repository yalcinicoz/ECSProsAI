// DHL/MNG Kargo — eski projeden ÇALIŞAN entegrasyon örneği (kullanıcı paylaştı, 2026-07-29).
// KG1 DHL adapter'ının birincil referansı. Taşınacağı yer: docs/APIDocs/MNGKargoAPIDocs/
// (klasör root'a ait; `sudo chown -R yalcin:yalcin /opt/ECSProsAI/docs/APIDocs` sonrası taşınır).
//
// Doğruladığı sözleşme:
// 1) TOKEN: POST https://api.mngkargo.com.tr/mngapi/api/token
//    Header: X-IBM-Client-Id, X-IBM-Client-Secret, accept: application/json
//    Body: { customerNumber, password, identityType: 1 }   // 1: TCKN, 2: Vergi No
//    Yanıt: JwtToken { jwt } → sonraki çağrılarda Authorization: Bearer {jwt}
// 2) GÖNDERİ: POST https://api.mngkargo.com.tr/mngapi/api/standardcmdapi/createOrder
//    (aynı IBM header'ları + Bearer) — payload aşağıdaki SendShipping'de.
//    Başarı: [ { orderInvoiceId, orderInvoiceDetailId } ] · Hata: { error: { code, description } }
//    Not: barcode BİZİM ürettiğimiz kod; MNG kendi gönderi numarasını (MNG_GONDERI_NO)
//    ayrıca üretir — takip linki MNG numarasıyla kurulur.
// 3) TAKİP: eski SOAP servis (MusteriKargoSiparisSoapClient.KargoTakipByReferans
//    [user, password, referansNo]) — hareket dataset'i: MNG_HAREKET_TANIM, ISLEM_BIRIMI,
//    ISLEM_TARIH_SAAT, ALICI_SUBE, TESLIM_ALAN, MNG_GONDERI_NO. KG1'de REST Standard
//    Query API tercih edilecek; bu SOAP yedek referans.
//
// Açık kalanlar (APIZone'dan sayfa/Swagger gerekli): cancelOrder (taşıyıcı değişimi),
// Standard Query uçları, enum anlamları (paymentType/shipmentServiceType/packagingType/
// deliveryType/smsPreference1-2-3 değer tabloları — özellikle kapıda NAKİT vs KART ayrımı).

public JwtToken GetToken(DataRow rowKargoHesaplari)
{
    string vUser = rowKargoHesaplari["koApiUser"].ToString();        //ödeme şekli ayrımı olmadığı için default koApiUser kullanıldı
    string vPassword = rowKargoHesaplari["koApiSecret"].ToString();    //ödeme şekli ayrımı olmadığı için default koApiSecret kullanıldı
    string vAPIClientId = rowKargoHesaplari["deger4"].ToString(); // API Client ID
    string vAPIClientSecret = rowKargoHesaplari["deger5"].ToString(); // API Client Secret
    dynamic customerData = new
    {
        customerNumber = vUser,
        password = vPassword,
        identityType = 1 // 1: TCKN, 2: Vergi No
    };
    string postData = Newtonsoft.Json.JsonConvert.SerializeObject(customerData);
    var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mngkargo.com.tr/mngapi/api/token");
    request.Headers.Add("X-IBM-Client-Id", vAPIClientId);
    request.Headers.Add("X-IBM-Client-Secret", vAPIClientSecret);
    request.Headers.Add("accept", "application/json");
    var content = new StringContent(postData, null, "application/json");
    request.Content = content;
    var response = client.Send(request);
    string result = response.Content.ReadAsStringAsync().Result;
    return Newtonsoft.Json.JsonConvert.DeserializeObject<JwtToken>(result);
}

public string SendShipping(int pOrderId, DataRow rowKargoHesaplari, int oldCourierId, string personnelId, bool forChange = false)
{
    // ... (sipariş yükleme + kargo-değişti kontrolü kısaltıldı)
    DHLOrder dHLOrder = new DHLOrder
    {
        order = new DHLShippingOrder
        {
            referenceId = order.sourcePlatformOrderNumber,
            barcode = order.shippingBarcode,
            billOfLandingId = order.shippingBarcode,
            isCOD = order.payableAmount > 0 ? 1 : 0,
            codAmount = order.payableAmount.ToString().Replace(",", "."), // MNG Kargo için kuruş cinsinden gönderiyoruz
            shipmentServiceType = 1, // Normal gönderi
            packagingType = 1, // Standart paket
            content = "Tekstil",
            smsPreference1 = order.kargoSMSGonder == 2 ? 1 : 0,
            smsPreference2 = 0,
            smsPreference3 = 0,
            paymentType = 1,//order.paymentTypeId == 2 ? 1 : (order.paymentTypeId == 3 ? 2 : 0), // Kapıda ödeme veya kredi kartı
            deliveryType = 1, // Normal teslimat
            description = "Sipariş No: " + order.shippingBarcode,
            marketPlaceShortCode = "",
            marketPlaceSaleCode = ""
        },
        orderPieceList = new System.Collections.Generic.List<DHLOrderPieceList>
        {
            new DHLOrderPieceList
            {
                barcode = order.shippingBarcode,
                desi = (int)order.desi,
                //kg = (int)(order.weight * 1000), // MNG Kargo için gram cinsinden gönderiyoruz
                content = "Tekstil"
            }
        },
        recipient = new DHLRecipient
        {
            customerId = "",
            refCustomerId = order.member.memberId.ToString(),
            cityCode = 0, // MNG Kargo için şehir kodu kullanılmıyor
            cityName = order.shippingAddress.cityName,
            districtCode = 0, // MNG Kargo için ilçe kodu kullanılmıyor
            districtName = order.shippingAddress.districtName,
            address = order.shippingAddress.addressDetail + " " + order.shippingAddress.neighborhoodName,
            bussinessPhoneNumber = order.member.phone,
            email = ECSGYE.Common.DataProcsStatic.IsValidEmail(order.member.email) ? order.member.email : "yok@hotmail.com",
            taxOffice = order.shippingAddress.taxOffice,
            taxNumber = order.shippingAddress.taxNumber,
            fullName = order.shippingAddress.contactFirstName + " " + order.shippingAddress.contactLastName,
            homePhoneNumber = "",
            mobilePhoneNumber = order.shippingAddress.phone
        }
    };
    string url = "https://api.mngkargo.com.tr/mngapi/api/standardcmdapi/createOrder";
    // Header: X-IBM-Client-Id + X-IBM-Client-Secret + Authorization: Bearer {jwt}
    // Başarı: List<DHLCreateOrderSuccessResponse> → orderInvoiceId-orderInvoiceDetailId
    // Hata: DHLCreateOrderErrorResponse → error.code + error.description
    // (eski projedeki MySQL güncelleme kuyruğu kısaltıldı — yeni sistemde gönderim kaydı modeli kullanılacak)
    return "OK";
}

// 3) TAKİP (eski SOAP — KG1'de REST Standard Query tercih; alan adları referans):
//    service.KargoTakipByReferans(user, password, order.sourcePlatformOrderNumber)
//    → DataSet: REFERANS_NO, MNG_GONDERI_NO, MNG_HAREKET_TANIM, ISLEM_BIRIMI,
//      ISLEM_TARIH_SAAT, ALICI_SUBE, TESLIM_ALAN (varsa teslim alan kişi)
//    Takip linki: https://kargotakip.mngkargo.com.tr/?takipNo={MNG_GONDERI_NO}
