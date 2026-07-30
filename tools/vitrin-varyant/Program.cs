// Mevcut media/vitrin görselleri için tek seferlik responsive varyant üretimi (A fazı).
// API'deki VitrinGorselVaryantlari ile aynı kural: _w480/_w800/_w1200/_w1920.webp, q78,
// yalnız orijinalden dar genişlikler. Var olan varyantı atlar (yeniden çalıştırılabilir).
using System.Text.RegularExpressions;
using ImageMagick;

var kok = args.Length > 0 ? args[0] : "/opt/ECSProsAI/media/vitrin";
int[] genislikler = [480, 800, 1200, 1920];
var varyantDeseni = new Regex(@"_w\d+\.webp$", RegexOptions.IgnoreCase);

int uretilen = 0, atlanan = 0, hatali = 0, dosyaSayisi = 0;
foreach (var dosya in Directory.EnumerateFiles(kok, "*.*", SearchOption.AllDirectories))
{
    var uzanti = Path.GetExtension(dosya).ToLowerInvariant();
    if (uzanti is not (".jpg" or ".jpeg" or ".png" or ".webp")) continue;
    if (varyantDeseni.IsMatch(dosya)) continue; // varyantın varyantı üretilmez
    dosyaSayisi++;
    try
    {
        using var gorsel = new MagickImage(dosya);
        var dizin = Path.GetDirectoryName(dosya)!;
        var adsiz = Path.GetFileNameWithoutExtension(dosya);
        foreach (var w in genislikler.Where(g => g < gorsel.Width))
        {
            var hedef = Path.Combine(dizin, $"{adsiz}_w{w}.webp");
            if (File.Exists(hedef)) { atlanan++; continue; }
            using var kopya = (MagickImage)gorsel.Clone();
            kopya.Resize(new MagickGeometry((uint)w, 0));
            kopya.Quality = 78;
            kopya.Format = MagickFormat.WebP;
            kopya.Write(hedef);
            uretilen++;
        }
    }
    catch (Exception ex)
    {
        hatali++;
        Console.WriteLine($"HATA: {dosya} — {ex.Message}");
    }
}
Console.WriteLine($"Bitti: {dosyaSayisi} kaynak görsel tarandı, {uretilen} varyant üretildi, {atlanan} zaten vardı, {hatali} hata.");
