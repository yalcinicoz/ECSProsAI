using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Npgsql;

// ─── ayarlar ─────────────────────────────────────────────────────────────────
var v3Conn = Environment.GetEnvironmentVariable("V3CONN") ?? "";
var pgConn = Environment.GetEnvironmentVariable("PGCONN") ?? "Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce";
const string Target = "erp:nebim";
var tr = CultureInfo.GetCultureInfo("tr-TR");

if (args.Length == 0) { Console.WriteLine("komut: discover | groups --type N | groups --procedure | plan --type N | apply --type N [--create-missing]"); return 1; }
var cmd = args[0];
int? typeCode = ArgInt("--type");
bool useProc = args.Contains("--procedure");
bool createMissing = args.Contains("--create-missing");
var skipCodes = (Array.IndexOf(args, "--skip") is var si && si >= 0 && si + 1 < args.Length ? args[si + 1] : "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
if (string.IsNullOrWhiteSpace(v3Conn)) { Console.Error.WriteLine("V3CONN ortam değişkeni boş."); return 2; }

switch (cmd)
{
    case "discover": await DiscoverAsync(); break;
    case "groups": foreach (var g in await ReadGroupsAsync()) Console.WriteLine($"{g.Code}\t{g.Name}\t{g.ItemCount}"); break;
    case "plan": await PlanAsync(apply: false); break;
    case "apply": await PlanAsync(apply: true); break;
    default: Console.WriteLine("bilinmeyen komut"); return 1;
}
return 0;

int? ArgInt(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : null; }
static string Norm(string s) => string.Join(' ', s.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")).Split(' ', StringSplitOptions.RemoveEmptyEntries));
static string Slug(string s)
{
    var map = new Dictionary<char, char> { ['ç']='c',['Ç']='c',['ğ']='g',['Ğ']='g',['ı']='i',['İ']='i',['ö']='o',['Ö']='o',['ş']='s',['Ş']='s',['ü']='u',['Ü']='u' };
    var sb = new StringBuilder(); foreach (var c in s) sb.Append(map.TryGetValue(c, out var m) ? m : c);
    var r = Regex.Replace(sb.ToString().ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
    return Regex.Replace(r, "_+", "_");
}

// ─── V3 okuma ────────────────────────────────────────────────────────────────
async Task DiscoverAsync()
{
    await using var c = new SqlConnection(v3Conn); await c.OpenAsync();
    await using var q = new SqlCommand("""
        SELECT t.AttributeTypeCode, t.AttributeTypeDescription,
               (SELECT COUNT(DISTINCT a.ItemCode) FROM prItemAttribute a WITH (NOLOCK) WHERE a.AttributeTypeCode=t.AttributeTypeCode) urun,
               (SELECT COUNT(*) FROM cdItemAttributeDesc d WITH (NOLOCK) WHERE d.AttributeTypeCode=t.AttributeTypeCode AND d.ItemTypeCode=1 AND d.LangCode='TR') deger,
               (SELECT TOP 1 d.AttributeDescription FROM cdItemAttributeDesc d WITH (NOLOCK) WHERE d.AttributeTypeCode=t.AttributeTypeCode AND d.ItemTypeCode=1 AND d.LangCode='TR' ORDER BY d.AttributeCode) ornek
          FROM cdItemAttributeTypeDesc t WITH (NOLOCK)
         WHERE t.ItemTypeCode=1 AND t.LangCode='TR'
         ORDER BY t.AttributeTypeCode
        """, c) { CommandTimeout = 300 };
    await using var r = await q.ExecuteReaderAsync();
    Console.WriteLine("TipKodu\tTipAdı\tÜrün\tDeğer\tÖrnek değerler");
    while (await r.ReadAsync())
        Console.WriteLine($"{r[0]}\t{r[1]}\t{r[2]}\t{r[3]}\t{(r.IsDBNull(4) ? "" : r.GetString(4))}");
}

async Task<List<(string Code, string Name, int ItemCount)>> ReadGroupsAsync()
{
    var list = new List<(string, string, int)>();
    await using var c = new SqlConnection(v3Conn); await c.OpenAsync();
    if (useProc)
    {
        // Katalog prosedürü: geniş pencere; yalnız ad döner (kod = ad)
        await using var p = new SqlCommand("jld_Appurunler", c) { CommandType = System.Data.CommandType.StoredProcedure, CommandTimeout = 600 };
        p.Parameters.AddWithValue("@olusturmaTarihi", new DateTime(2000, 1, 1));
        p.Parameters.AddWithValue("@guncellemeTarihi", DBNull.Value);
        p.Parameters.AddWithValue("@isInsert", false);
        p.Parameters.AddWithValue("@checkBoth", false);
        p.Parameters.AddWithValue("@ItemCode", DBNull.Value);
        var sayac = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var r = await p.ExecuteReaderAsync();
        var idx = r.GetOrdinal("urunGrubu");
        while (await r.ReadAsync()) { var g = r.IsDBNull(idx) ? "" : r.GetString(idx).Trim(); if (g.Length == 0) continue; sayac[g] = sayac.GetValueOrDefault(g) + 1; }
        foreach (var kv in sayac.OrderByDescending(x => x.Value)) list.Add((kv.Key, kv.Key, kv.Value));
        return list;
    }
    if (typeCode is null) throw new Exception("--type N gerekli (discover ile bul) ya da --procedure");
    await using var q = new SqlCommand("""
        SELECT CONVERT(varchar(50), d.AttributeCode), d.AttributeDescription,
               (SELECT COUNT(DISTINCT a.ItemCode) FROM prItemAttribute a WITH (NOLOCK) WHERE a.AttributeTypeCode=d.AttributeTypeCode AND a.AttributeCode=d.AttributeCode) urun
          FROM cdItemAttributeDesc d WITH (NOLOCK)
         WHERE d.AttributeTypeCode=@t AND d.ItemTypeCode=1 AND d.LangCode='TR'
         ORDER BY d.AttributeDescription
        """, c) { CommandTimeout = 300 };
    q.Parameters.AddWithValue("@t", typeCode.Value);
    await using var rr = await q.ExecuteReaderAsync();
    while (await rr.ReadAsync()) { var code = rr.GetString(0).Trim(); var name = rr.GetString(1).Trim(); if (code.Length > 0 && name.Length > 0) list.Add((code, name, rr.GetInt32(2))); }
    return list;
}

// ─── eşleme planı / uygulama ─────────────────────────────────────────────────
async Task PlanAsync(bool apply)
{
    var nebim = await ReadGroupsAsync();
    await using var pg = new NpgsqlConnection(pgConn); await pg.OpenAsync();

    // bizim gruplar
    var ours = new List<(Guid Id, string Code, string Name)>();
    await using (var q = new NpgsqlCommand("SELECT \"Id\",\"Code\",COALESCE(\"NameI18n\"->>'tr','') FROM definition.product_groups WHERE NOT \"IsDeleted\"", pg))
    await using (var r = await q.ExecuteReaderAsync()) while (await r.ReadAsync()) ours.Add((r.GetGuid(0), r.GetString(1), r.GetString(2)));
    var byCode = ours.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    var byName = ours.GroupBy(x => Norm(x.Name)).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());

    // mevcut erp:nebim eşlemeleri (ERP kodu → grup)
    var mevcut = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    await using (var q = new NpgsqlCommand("SELECT \"TargetExternalId\",\"ProductGroupId\" FROM integration.marketplace_category_mappings WHERE \"Marketplace\"=@m AND NOT \"IsDeleted\" AND \"Status\"='active' AND \"MappingKind\"='direct'", pg))
    { q.Parameters.AddWithValue("m", Target); await using var r = await q.ExecuteReaderAsync(); while (await r.ReadAsync()) mevcut.TryAdd(r.GetString(0), r.GetGuid(1)); }

    // appsettings sözlükleri (ProductGroupCodes / PrefixCodes)
    var cfg = LoadConfigMaps();

    var plan = new List<(string Code, string Name, int Items, Guid? GroupId, string? GroupCode, string Method)>();
    var skipped = new List<(string Code, string Name)>();
    foreach (var g0 in nebim)
    {
        if (skipCodes.Contains(g0.Code)) { Console.WriteLine($"(atlandı: {g0.Code} {g0.Name} — sözlüğe EŞLENMEMİŞ yazılır, ürünleri geçici gruba düşer)"); skipped.Add((g0.Code, TitleTr(g0.Name))); continue; }
        var g = (g0.Code, Name: TitleTr(g0.Name), g0.ItemCount);
        var n = Norm(g.Name);
        if (mevcut.TryGetValue(g.Code, out var m)) { var o = ours.First(x => x.Id == m); plan.Add((g.Code, g.Name, g.ItemCount, m, o.Code, "mevcut eşleme")); continue; }
        // 2026-09-06 kararı (katman yok, "Kot Ceket" kendi grubudur): ad birebir eşleşme config'in ÖNÜNDE
        if (byName.TryGetValue(n, out var on)) { plan.Add((g.Code, g.Name, g.ItemCount, on.Id, on.Code, "ad birebir")); continue; }
        if (cfg.Codes.TryGetValue(n, out var cc) && byCode.TryGetValue(cc, out var oc)) { plan.Add((g.Code, g.Name, g.ItemCount, oc.Id, oc.Code, "config ProductGroupCodes")); continue; }
        var pc = cfg.Prefix.Where(p => n == p.Key || n.StartsWith(p.Key + " ")).OrderByDescending(p => p.Key.Length).Select(p => p.Value).Distinct().ToArray();
        if (pc.Length == 1 && byCode.TryGetValue(pc[0], out var op)) { plan.Add((g.Code, g.Name, g.ItemCount, op.Id, op.Code, "config PrefixCodes")); continue; }
        plan.Add((g.Code, g.Name, g.ItemCount, null, null, "YENİ GRUP"));
    }

    Console.WriteLine("NebimKod\tNebimAd\tÜrün\tBizimGrup\tYöntem");
    foreach (var p in plan) Console.WriteLine($"{p.Code}\t{p.Name}\t{p.Items}\t{p.GroupCode ?? "-"}\t{p.Method}");
    Console.WriteLine($"\nToplam {plan.Count} Nebim grubu: eşleşen {plan.Count(p => p.GroupId != null)}, yeni açılacak {plan.Count(p => p.GroupId == null)}");
    if (!apply) return;

    await using var tx = await pg.BeginTransactionAsync();
    int sozluk = 0, esleme = 0, yeni = 0, sablon = 0;
    foreach (var p in plan)
    {
        var groupId = p.GroupId; var groupCode = p.GroupCode;
        if (groupId is null)
        {
            if (!createMissing) continue;
            // yeni grup: kod slug (çakışmada _2), ad; şablon = adı son sözcük(ler)iyle eşleşen mevcut grup (Kot Ceket → Ceket)
            var slug = Slug(p.Name); var baseSlug = slug; var k = 2;
            while (byCode.ContainsKey(slug)) slug = $"{baseSlug}_{k++}";
            var id = Guid.NewGuid();
            await Exec(pg, tx, """
                INSERT INTO definition.product_groups ("Id","Code","NameI18n","IsActive","SortOrder","CreatedAt","IsDeleted")
                VALUES (@id,@code,@name::jsonb,true,1,timezone('utc',now()),false)
                """, ("id", id), ("code", slug), ("name", System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["tr"] = p.Name })));
            byCode[slug] = (id, slug, p.Name); ours.Add((id, slug, p.Name)); yeni++;
            groupId = id; groupCode = slug;
            var template = SablonBul(p.Name, ours.Where(o => o.Id != id).ToList());
            if (template is { } t)
            {
                sablon += await Exec(pg, tx, """
                    INSERT INTO definition.product_group_attributes ("Id","ProductGroupId","AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","DefaultAttributeValueId","CreatedAt","IsDeleted")
                    SELECT gen_random_uuid(), @yeni, "AttributeTypeId","IsVariant","IsRequired","IsPrimaryAxis","SortOrder","DefaultAttributeValueId", timezone('utc',now()), false
                      FROM definition.product_group_attributes WHERE "ProductGroupId"=@kaynak AND NOT "IsDeleted"
                    """, ("yeni", id), ("kaynak", t.Id));
                await Exec(pg, tx, """
                    INSERT INTO definition.product_group_axis_sub_attributes ("Id","ProductGroupId","AxisAttributeTypeId","SubAttributeTypeId","IsRequired","SortOrder","CreatedAt","IsDeleted")
                    SELECT gen_random_uuid(), @yeni, "AxisAttributeTypeId","SubAttributeTypeId","IsRequired","SortOrder", timezone('utc',now()), false
                      FROM definition.product_group_axis_sub_attributes WHERE "ProductGroupId"=@kaynak AND NOT "IsDeleted"
                    """, ("yeni", id), ("kaynak", t.Id));
                Console.WriteLine($"  + yeni grup {slug} \"{p.Name}\" (şablon: {t.Code} \"{t.Name}\")");
            }
            else Console.WriteLine($"  + yeni grup {slug} \"{p.Name}\" (şablon YOK — panelden özellik ekleyin)");
        }
        // sözlük
        sozluk += await Exec(pg, tx, """
            INSERT INTO integration.erp_reference_items ("Id","TargetSystem","Kind","Code","Name","IsActive","FirstSeenAt","LastSeenAt","Source","CreatedAt","IsDeleted","MappedTargetKind","MappedTargetId","MappedTargetLabel")
            VALUES (gen_random_uuid(), @t, 'product_group', @code, @name, true, now(), now(), 'tool', now(), false, 'product_group', @gid, @glabel)
            ON CONFLICT ("TargetSystem","Kind","Code") WHERE "IsDeleted"=false
            DO UPDATE SET "Name"=EXCLUDED."Name", "LastSeenAt"=now(), "IsActive"=true, "UpdatedAt"=now(),
                          "MappedTargetKind"='product_group', "MappedTargetId"=EXCLUDED."MappedTargetId", "MappedTargetLabel"=EXCLUDED."MappedTargetLabel"
            """, ("t", Target), ("code", p.Code), ("name", p.Name), ("gid", groupId!.Value), ("glabel", $"{ours.First(o => o.Id == groupId).Name} [{groupCode}]"));
        // eşleme (bizim grup → ERP kodu); grup için aktif direct satır varsa dokunma
        if (!mevcut.ContainsValue(groupId.Value))
        {
            esleme += await Exec(pg, tx, """
                INSERT INTO integration.marketplace_category_mappings ("Id","Marketplace","ProductGroupId","FirmPlatformId","MappingKind","TargetExternalId","TargetName","TargetPath","Status","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(), @m, @gid, NULL, 'direct', @code, @name, @path, 'active', now(), false
                 WHERE NOT EXISTS (SELECT 1 FROM integration.marketplace_category_mappings x WHERE x."Marketplace"=@m AND x."ProductGroupId"=@gid AND x."FirmPlatformId" IS NULL AND NOT x."IsDeleted")
                """, ("m", Target), ("gid", groupId.Value), ("code", p.Code), ("name", p.Name), ("path", $"{p.Name} [{p.Code}]"));
            mevcut[p.Code] = groupId.Value;
        }
    }
    // atlananlar: sözlükte eşlenmemiş satır (panel "Eşlenmemiş" kuyruğu); var olan satıra dokunulmaz
    int eslenmemis = 0;
    foreach (var (code, name) in skipped)
        eslenmemis += await Exec(pg, tx, """
            INSERT INTO integration.erp_reference_items ("Id","TargetSystem","Kind","Code","Name","IsActive","FirstSeenAt","LastSeenAt","Source","CreatedAt","IsDeleted")
            SELECT gen_random_uuid(), @t, 'product_group', @code, @name, true, now(), now(), 'tool', now(), false
             WHERE NOT EXISTS (SELECT 1 FROM integration.erp_reference_items x WHERE x."TargetSystem"=@t AND x."Kind"='product_group' AND x."Code"=@code AND NOT x."IsDeleted")
            """, ("t", Target), ("code", code), ("name", name));
    await tx.CommitAsync();
    Console.WriteLine($"\nYAZILDI: sözlük {sozluk}, yeni eşleme {esleme}, yeni grup {yeni} (şablon özelliği {sablon}), eşlenmemiş sözlük satırı {eslenmemis}. Yeni grup açıldıysa 'Ürün Grubu' seçim özelliği/varsayılanı seed'de (restart) tamamlanır.");
}

(Guid Id, string Code, string Name)? SablonBul(string name, List<(Guid Id, string Code, string Name)> ours)
{
    // "Kot Ceket" → "Ceket"; "Tesettür Triko Hırka" → "Hırka" (son sözcük), sonra son iki sözcük; olmazsa ilk sözcük
    // ("Pijama Takımı" → Pijama); yoksa null
    var w = Norm(name).Split(' ');
    var byName = ours.GroupBy(x => Norm(x.Name)).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
    for (var take = Math.Min(2, w.Length - 1); take >= 1; take--)
    {
        var suffix = string.Join(' ', w.Skip(w.Length - take));
        if (byName.TryGetValue(suffix, out var hit)) return hit;
    }
    if (w.Length > 1 && byName.TryGetValue(w[0], out var first)) return first;
    return null;
}

static string TitleTr(string s)
{
    // "şişme mont" → "Şişme Mont", "DENİZ ŞORT" → "Deniz Şort"; kısaltma gibi 2-3 harfli tamamı büyükler korunur
    var tr = CultureInfo.GetCultureInfo("tr-TR");
    var words = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    for (var i = 0; i < words.Length; i++)
    {
        var w = words[i];
        if (w.Length <= 3 && w.All(char.IsUpper)) continue;
        var lower = w.ToLower(tr);
        words[i] = lower.Length == 0 ? w : (lower[0] == 'i' ? "İ" : lower[..1].ToUpper(tr)) + lower[1..];
    }
    return string.Join(' ', words);
}

(Dictionary<string, string> Codes, Dictionary<string, string> Prefix) LoadConfigMaps()
{
    var codes = new Dictionary<string, string>(); var prefix = new Dictionary<string, string>();
    try
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../../src/ECSPros.Api/appsettings.json");
        if (!File.Exists(path)) path = "/opt/ECSProsAI/src/ECSPros.Api/appsettings.json";
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.TryGetProperty("ErpSource", out var erp))
        {
            if (erp.TryGetProperty("ProductGroupCodes", out var pgc)) foreach (var p in pgc.EnumerateObject()) codes[Norm(p.Name)] = p.Value.GetString() ?? "";
            if (erp.TryGetProperty("ProductGroupPrefixCodes", out var ppc)) foreach (var p in ppc.EnumerateObject()) prefix[Norm(p.Name)] = p.Value.GetString() ?? "";
        }
    }
    catch (Exception ex) { Console.Error.WriteLine("appsettings okunamadı: " + ex.Message); }
    return (codes, prefix);
}

static async Task<int> Exec(NpgsqlConnection pg, NpgsqlTransaction tx, string sql, params (string, object)[] prms)
{
    await using var c = new NpgsqlCommand(sql, pg, tx);
    foreach (var (k, v) in prms) c.Parameters.AddWithValue(k, v);
    return await c.ExecuteNonQueryAsync();
}
