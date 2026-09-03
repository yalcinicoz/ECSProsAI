using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace ECSPros.Api.Services.ErpSource;

/// <summary>V3 ERP'nin katalog ve fiyat prosedürlerini salt-okuma çağırır. Stok kaynağı değildir.</summary>
public sealed class SqlServerErpSourceReader(ErpSourceOptions options)
    : IErpSourceReader, IErpProductAttributeBatchReader
{
    private readonly TimeZoneInfo _sourceTimeZone = ResolveTimeZone(options.SourceTimeZoneId);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ConnectionString);

    public async Task<IReadOnlyList<ErpProductRow>> ReadProductsAsync(DateTime sinceUtc, CancellationToken ct)
    {
        EnsureConfigured();
        EnsureProcedureName(options.CatalogProcedure);
        var sourceSince = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(sinceUtc), _sourceTimeZone);
        var rows = new Dictionary<string, ErpProductRow>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);

        // Eski kod yeni ve güncellenen ürünleri iki ayrı çağrıyla alıyordu. Aynı kesimi
        // örtüşmeli çağırıp ürün koduyla birleştiriyoruz; böylece sınır zamanı kaybolmaz.
        await ReadProductSliceAsync(connection, sourceSince, creationSlice: true, rows, ct);
        await ReadProductSliceAsync(connection, sourceSince, creationSlice: false, rows, ct);
        return rows.Values.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<ErpVariantRow>> ReadVariantsAsync(string productCode, CancellationToken ct)
    {
        EnsureConfigured();
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        return await ReadVariantsAsync(connection, productCode, ct);
    }

    private async Task<IReadOnlyList<ErpVariantRow>> ReadVariantsAsync(
        SqlConnection connection, string productCode, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT b.Barcode,c.ColorCode,dbo.BKUCUK(c.ColorDescription) AS ColorDescription,v.ItemDim1Code,
                   v.CreatedDate,v.LastUpdatedDate
              FROM prItemVariant v WITH (NOLOCK)
              JOIN prItemBarcode b WITH (NOLOCK)
                ON v.ItemCode=b.ItemCode AND v.ColorCode=b.ColorCode AND v.ItemDim1Code=b.ItemDim1Code
              JOIN cdColorDesc c WITH (NOLOCK) ON c.ColorCode=v.ColorCode
             WHERE v.ItemTypeCode=1 AND b.ItemCode=@urunKodu
             ORDER BY c.ColorDescription,v.ItemDim1Code
            """, connection) { CommandTimeout = options.CommandTimeoutSeconds };
        command.Parameters.Add(new SqlParameter("@urunKodu", SqlDbType.VarChar, 20) { Value = productCode });
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ErpVariantRow>();
        while (await reader.ReadAsync(ct))
        {
            var barcode = GetString(reader, "Barcode");
            if (string.IsNullOrWhiteSpace(barcode)) continue;
            var attributes = new List<ErpVariantAttributeRow>(3);
            var colorCode = GetString(reader, "ColorCode")?.Trim();
            var colorName = GetString(reader, "ColorDescription")?.Trim();
            var sizeCode = GetString(reader, "ItemDim1Code")?.Trim();
            if (!string.IsNullOrWhiteSpace(colorName)) attributes.Add(new(1, colorName, colorCode));
            if (!string.IsNullOrWhiteSpace(sizeCode)) attributes.Add(new(2, sizeCode, sizeCode));
            result.Add(new(barcode.Trim(), GetUtc(reader, "CreatedDate"),
                GetUtc(reader, "LastUpdatedDate"), attributes));
        }
        return result;
    }

    public async Task<IReadOnlyList<ErpProductAttributeRow>> ReadProductAttributesAsync(string productCode, CancellationToken ct)
    {
        EnsureConfigured();
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        return await ReadProductAttributesAsync(connection, productCode, ct);
    }

    private async Task<IReadOnlyList<ErpProductAttributeRow>> ReadProductAttributesAsync(
        SqlConnection connection, string productCode, CancellationToken ct)
    {
        var rows = await ReadProductAttributesAsync(connection, [productCode], ct);
        return rows.GetValueOrDefault(productCode) ?? [];
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ErpProductAttributeRow>>> ReadProductAttributesAsync(
        IReadOnlyCollection<string> productCodes, CancellationToken ct)
    {
        EnsureConfigured();
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        return await ReadProductAttributesAsync(connection, productCodes, ct);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<ErpProductAttributeRow>>> ReadProductAttributesAsync(
        SqlConnection connection, IReadOnlyCollection<string> productCodes, CancellationToken ct)
    {
        var codes = productCodes.Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var typeCodes = options.ProductAttributeTypeCodes.Keys
            .Select(x => int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : (int?)null)
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().OrderBy(x => x).ToArray();
        if (codes.Length == 0 || typeCodes.Length == 0)
            return new Dictionary<string, IReadOnlyList<ErpProductAttributeRow>>(StringComparer.OrdinalIgnoreCase);

        var codeParameters = codes.Select((_, i) => $"@code{i}").ToArray();
        var typeParameters = typeCodes.Select((_, i) => $"@type{i}").ToArray();
        await using var command = new SqlCommand($"""
            SELECT a.ItemCode,CONVERT(varchar(20),a.AttributeTypeCode),
                   t.AttributeTypeDescription,d.AttributeDescription,CONVERT(varchar(100),a.AttributeCode)
              FROM prItemAttribute a WITH (NOLOCK)
              JOIN cdItemAttributeTypeDesc t WITH (NOLOCK)
                ON t.AttributeTypeCode=a.AttributeTypeCode AND t.ItemTypeCode=1 AND t.LangCode='TR'
              JOIN cdItemAttributeDesc d WITH (NOLOCK)
                ON d.AttributeTypeCode=a.AttributeTypeCode AND d.AttributeCode=a.AttributeCode
               AND d.ItemTypeCode=1 AND d.LangCode='TR'
             WHERE a.ItemCode IN ({string.Join(',', codeParameters)})
               AND a.AttributeTypeCode IN ({string.Join(',', typeParameters)})
             ORDER BY a.ItemCode,a.AttributeTypeCode,a.AttributeCode
            """, connection) { CommandTimeout = options.CommandTimeoutSeconds };
        for (var i = 0; i < codes.Length; i++)
            command.Parameters.Add(new SqlParameter(codeParameters[i], SqlDbType.VarChar, 20) { Value = codes[i] });
        for (var i = 0; i < typeCodes.Length; i++)
            command.Parameters.Add(new SqlParameter(typeParameters[i], SqlDbType.Int) { Value = typeCodes[i] });

        var result = new Dictionary<string, List<ErpProductAttributeRow>>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(0).Trim();
            if (!result.TryGetValue(code, out var rows)) result[code] = rows = [];
            rows.Add(new(reader.GetString(1).Trim(), reader.GetString(3).Trim(),
                reader.GetString(2).Trim(), reader.GetString(4).Trim()));
        }
        return result.ToDictionary(x => x.Key, x => (IReadOnlyList<ErpProductAttributeRow>)x.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ErpProductSnapshot?> ReadProductSnapshotAsync(string productCode, CancellationToken ct)
    {
        EnsureConfigured();
        var code = productCode.Trim();
        if (code.Length == 0) return null;
        var rows = new Dictionary<string, ErpProductRow>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        await ReadProductSliceAsync(connection, DateTime.UtcNow, false, rows, ct, code);
        if (!rows.TryGetValue(code, out var product)) return null;
        var variants = await ReadVariantsAsync(connection, code, ct);
        var attributes = await ReadProductAttributesAsync(connection, code, ct);
        var supplier = await ReadSupplierAsync(connection, code, ct);
        return new(product, variants, attributes, supplier);
    }

    public async Task<string?> ResolveProductCodeByBarcodeAsync(string barcode, CancellationToken ct)
    {
        EnsureConfigured();
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            SELECT TOP (1) ItemCode FROM prItemBarcode WITH (NOLOCK) WHERE Barcode=@barcode
            """, connection) { CommandTimeout = options.CommandTimeoutSeconds };
        command.Parameters.Add(new SqlParameter("@barcode", SqlDbType.VarChar, 100) { Value = barcode.Trim() });
        return (await command.ExecuteScalarAsync(ct) as string)?.Trim();
    }

    private async Task<ErpSupplierRow?> ReadSupplierAsync(
        SqlConnection connection, string productCode, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT TOP (1) CONVERT(varchar(50),att.AttributeCode),d.AttributeDescription
              FROM prItemAttribute att WITH (NOLOCK)
              JOIN cdItemAttributeDesc d WITH (NOLOCK)
                ON d.AttributeTypeCode=att.AttributeTypeCode AND d.AttributeCode=att.AttributeCode
             WHERE att.ItemCode=@code AND att.AttributeTypeCode=3
               AND d.ItemTypeCode=1 AND d.LangCode='TR'
             ORDER BY att.AttributeCode
            """, connection) { CommandTimeout = options.CommandTimeoutSeconds };
        command.Parameters.Add(new SqlParameter("@code", SqlDbType.VarChar, 20) { Value = productCode });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0).Trim(), reader.GetString(1).Trim()) : null;
    }

    private async Task ReadProductSliceAsync(SqlConnection connection, DateTime sinceSource,
        bool creationSlice, IDictionary<string, ErpProductRow> rows, CancellationToken ct,
        string? itemCode = null)
    {
        await using var command = StoredProcedure(connection, options.CatalogProcedure);
        command.Parameters.Add(new SqlParameter("@olusturmaTarihi", SqlDbType.DateTime)
            { Value = creationSlice ? sinceSource : DBNull.Value });
        command.Parameters.Add(new SqlParameter("@guncellemeTarihi", SqlDbType.DateTime)
            { Value = creationSlice ? DBNull.Value : sinceSource });
        command.Parameters.Add(new SqlParameter("@isInsert", SqlDbType.Bit) { Value = false });
        command.Parameters.Add(new SqlParameter("@checkBoth", SqlDbType.Bit) { Value = false });
        command.Parameters.Add(new SqlParameter("@ItemCode", SqlDbType.VarChar, 20)
            { Value = (object?)itemCode ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = GetString(reader, "urunKodu");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var values = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in PriceColumns())
                values[column] = GetNullableDecimal(reader, column);

            var row = new ErpProductRow(
                code.Trim(),
                GetString(reader, "urunAdi")?.Trim() is { Length: > 0 } name ? name : code.Trim(),
                GetString(reader, "urunInternetAdi")?.Trim(),
                GetString(reader, "urunGrubu")?.Trim(),
                GetNullableDecimal(reader, "tozluSatisFiyati") ?? 0m,
                GetNullableDecimal(reader, "tozluAlisFiyati"),
                ParseTax(GetString(reader, "kdvOrani")),
                GetBool(reader, "interneteAcik"),
                GetUtc(reader, "olusturmaTarihi"),
                GetUtc(reader, "guncellemeTarihi"),
                values);

            if (!rows.TryGetValue(row.Code, out var old) || (row.UpdatedAtUtc ?? row.CreatedAtUtc) >= (old.UpdatedAtUtc ?? old.CreatedAtUtc))
                rows[row.Code] = row;
        }
    }

    private SqlCommand StoredProcedure(SqlConnection connection, string name) => new(name, connection)
    {
        CommandType = CommandType.StoredProcedure,
        CommandTimeout = options.CommandTimeoutSeconds
    };

    private IEnumerable<string> PriceColumns()
    {
        yield return "tozluAlisFiyati";
        yield return "tozluSatisFiyati";
        yield return "tozluListeFiyati";
        yield return "juludeSatisFiyati";
        yield return "juludeListeFiyati";
        yield return "BayiAlisFiyati";
        yield return "BayiSatisFiyati";
        foreach (var p in options.ChannelPrices.Values)
        {
            if (!string.IsNullOrWhiteSpace(p.PriceColumn)) yield return p.PriceColumn;
            if (!string.IsNullOrWhiteSpace(p.CompareAtPriceColumn)) yield return p.CompareAtPriceColumn;
        }
    }

    private void AddAttribute(SqlDataReader reader, ICollection<ErpVariantAttributeRow> target, string typeColumn, string valueColumn)
    {
        int typeId = GetInt(reader, typeColumn);
        string? value = GetString(reader, valueColumn);
        if (typeId > 0 && !string.IsNullOrWhiteSpace(value)) target.Add(new(typeId, value.Trim()));
    }

    private DateTime? GetUtc(SqlDataReader reader, string column)
    {
        int i = Ordinal(reader, column);
        if (i < 0 || reader.IsDBNull(i)) return null;
        var local = Convert.ToDateTime(reader.GetValue(i), CultureInfo.InvariantCulture);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, _sourceTimeZone);
    }

    internal static int ParseTax(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 20;
        value = value.Replace("%", "", StringComparison.Ordinal).Trim();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 20;
    }

    private static string? GetString(SqlDataReader r, string column)
    {
        int i = Ordinal(r, column);
        return i < 0 || r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);
    }

    private static int GetInt(SqlDataReader r, string column)
    {
        int i = Ordinal(r, column);
        return i < 0 || r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
    }

    private static decimal? GetNullableDecimal(SqlDataReader r, string column)
    {
        int i = Ordinal(r, column);
        return i < 0 || r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);
    }

    private static bool GetBool(SqlDataReader r, string column)
    {
        int i = Ordinal(r, column);
        if (i < 0 || r.IsDBNull(i)) return false;
        var v = r.GetValue(i);
        return v is bool b ? b : v.ToString() is "1" or "true" or "True";
    }

    private static int Ordinal(SqlDataReader r, string column)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (r.GetName(i).Equals(column, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static void EnsureProcedureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '.')))
            throw new InvalidOperationException("Geçersiz ERP prosedür adı.");
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("ERP kaynak bağlantısı yapılandırılmamış.");
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows geliştirici makinesi için eşdeğer ad; Linux production'da IANA kullanılır.
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch (Exception fallbackEx) when (fallbackEx is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
        }
    }
}
