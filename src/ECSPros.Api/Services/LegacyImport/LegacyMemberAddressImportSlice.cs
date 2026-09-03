using System.Globalization;
using System.Text.Json;
using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>Legacy üyeleri/adresleri yalnız kendi Legacy*Id kayıtlarına idempotent upsert eder.</summary>
public sealed class LegacyMemberAddressImportSlice(
    ILegacyMemberAddressReader reader,
    ICrmDbContext db,
    ILegacyImportCheckpointStore checkpoints,
    LegacyReadImportOptions options,
    ILogger<LegacyMemberAddressImportSlice> logger) : ILegacyCommerceImportSlice
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly TimeZoneInfo _sourceTimeZone = ResolveTimeZone(options.SourceTimeZoneId);
    public string Slice => LegacyImportSlices.Members;

    public async Task<LegacyImportSliceReport> RunAsync(CancellationToken ct)
    {
        try
        {
            var snapshot = await reader.ReadAsync(options.PlatformId, ct);
            var defaultGroup = await db.MemberGroups.AsNoTracking()
                .Where(x => x.IsDefault && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefaultAsync(ct);
            if (defaultGroup is null)
                return Fail("Hedefte aktif varsayılan üye grubu bulunamadı.");

            var targetMembers = await db.Members.IgnoreQueryFilters()
                .Where(x => x.LegacyMemberId != null)
                .ToDictionaryAsync(x => x.LegacyMemberId!.Value, ct);
            var emailRows = await db.Members.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Email != null && !x.IsDeleted)
                .Select(x => new { x.Email, x.Id }).ToListAsync(ct);
            var emailOwners = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in emailRows) emailOwners.TryAdd(row.Email!.Trim(), row.Id);
            var phoneRows = await db.Members.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Phone != null && !x.IsDeleted)
                .Select(x => new { x.Phone, x.Id }).ToListAsync(ct);
            var phoneOwners = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var row in phoneRows) phoneOwners.TryAdd(row.Phone!.Trim(), row.Id);

            var cities = await db.Cities.AsNoTracking().ToListAsync(ct);
            var cityByName = cities
                .Select(x => (Entity: x, Name: TurkishName(x.NameI18n)))
                .Where(x => x.Name.Length > 0)
                .GroupBy(x => Key(x.Name))
                .ToDictionary(x => x.Key, x => x.First().Entity);
            var districts = await db.Districts.AsNoTracking().ToListAsync(ct);
            var districtByName = districts
                .Select(x => (Entity: x, Name: TurkishName(x.NameI18n)))
                .Where(x => x.Name.Length > 0)
                .GroupBy(x => $"{x.Entity.CityId:N}|{Key(x.Name)}")
                .ToDictionary(x => x.Key, x => x.First().Entity);
            var neighborhoods = await db.Neighborhoods.AsNoTracking().ToListAsync(ct);
            var neighborhoodByName = neighborhoods
                .Select(x => (Entity: x, Name: TurkishName(x.NameI18n)))
                .Where(x => x.Name.Length > 0)
                .GroupBy(x => $"{x.Entity.DistrictId:N}|{Key(x.Name)}")
                .ToDictionary(x => x.Key, x => x.First().Entity);
            var country = await db.Countries.AsNoTracking()
                .OrderByDescending(x => x.Code == "TR")
                .ThenBy(x => x.SortOrder)
                .FirstOrDefaultAsync(ct);

            var skipped = 0;
            var memberIds = new Dictionary<int, Guid>();
            foreach (var source in snapshot.Members)
            {
                ct.ThrowIfCancellationRequested();
                targetMembers.TryGetValue(source.Id, out var member);
                if (member is { IsDeleted: true } || member?.AnonymizedAt is not null)
                {
                    skipped++;
                    continue;
                }

                if (member is null)
                {
                    member = new Member
                    {
                        MemberGroupId = defaultGroup.Id,
                        LegacyMemberId = source.Id,
                        CreatedAt = Utc(source.CreatedAt) ?? DateTime.UtcNow
                    };
                    db.Members.Add(member);
                    targetMembers[source.Id] = member;
                }

                ApplyMember(member, source, emailOwners, phoneOwners, cityByName);
                memberIds[source.Id] = member.Id;
            }

            var memberIdSet = memberIds.Values.ToHashSet();
            var targetAddresses = await db.Addresses.IgnoreQueryFilters()
                .Where(x => memberIdSet.Contains(x.MemberId))
                .ToListAsync(ct);
            var legacyAddresses = targetAddresses
                .Where(x => x.LegacyAddressId.HasValue)
                .ToDictionary(x => x.LegacyAddressId!.Value);
            var hasDefault = targetAddresses.Where(x => !x.IsDeleted && x.IsDefault)
                .Select(x => x.MemberId).ToHashSet();

            foreach (var source in snapshot.Addresses)
            {
                ct.ThrowIfCancellationRequested();
                if (!memberIds.TryGetValue(source.MemberId, out var memberId))
                {
                    skipped++;
                    continue;
                }

                legacyAddresses.TryGetValue(source.Id, out var address);
                if (address is { IsDeleted: true })
                {
                    skipped++;
                    continue;
                }

                if (address is null)
                {
                    var matches = targetAddresses
                        .Where(x => !x.IsDeleted && x.MemberId == memberId && x.LegacyAddressId is null
                            && AddressSignature(x) == AddressSignature(source))
                        .ToList();
                    if (matches.Count > 1)
                    {
                        logger.LogWarning("Legacy adres {LegacyAddressId} için birden fazla hedef eşleşmesi bulundu; atlandı.", source.Id);
                        skipped++;
                        continue;
                    }
                    address = matches.SingleOrDefault();
                    if (address is null)
                    {
                        address = new Address
                        {
                            MemberId = memberId,
                            CreatedAt = Utc(source.CreatedAt) ?? DateTime.UtcNow,
                            IsDefault = !hasDefault.Contains(memberId)
                        };
                        db.Addresses.Add(address);
                        targetAddresses.Add(address);
                    }
                    address.LegacyAddressId = source.Id;
                    legacyAddresses[source.Id] = address;
                }

                ApplyAddress(address, source, country, cityByName, districtByName, neighborhoodByName);
                if (address.IsDefault) hasDefault.Add(memberId);
            }

            var changed = PendingEntityChanges(db);

            if (!options.DryRun)
            {
                await db.SaveChangesAsync(ct);
                var watermark = snapshot.Members.Select(x => x.UpdatedAt ?? x.CreatedAt)
                    .Concat(snapshot.Addresses.Select(x => x.UpdatedAt ?? x.CreatedAt))
                    .Where(x => x.HasValue).Select(x => Utc(x)!.Value)
                    .DefaultIfEmpty(DateTime.UtcNow).Max();
                var lastId = snapshot.Members.Select(x => (long)x.Id)
                    .Concat(snapshot.Addresses.Select(x => (long)x.Id)).DefaultIfEmpty().Max();
                await checkpoints.SaveSuccessAsync(Slice, options.PlatformId, watermark, lastId, ct);
            }

            return new(Slice, true, options.DryRun, changed, skipped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy üye/adres importu başarısız");
            if (!options.DryRun)
            {
                try { await checkpoints.SaveErrorAsync(Slice, options.PlatformId, ex.Message, ct); }
                catch (Exception logEx) { logger.LogWarning(logEx, "Legacy üye/adres checkpoint hatası yazılamadı"); }
            }
            return Fail(ex.Message);
        }
    }

    private LegacyImportSliceReport Fail(string error) => new(Slice, false, options.DryRun, 0, 0, error);

    private static void ApplyMember(
        Member member, LegacyMemberSourceRow source,
        IDictionary<string, Guid> emailOwners, IDictionary<string, Guid> phoneOwners,
        IReadOnlyDictionary<string, City> cities)
    {
        var email = NormalizeEmail(source.Email);
        if (email is not null && emailOwners.TryGetValue(email, out var emailOwner) && emailOwner != member.Id)
            email = null;
        var phone = NormalizePhone(source.Phone);
        if (phone is not null && phoneOwners.TryGetValue(phone, out var phoneOwner) && phoneOwner != member.Id)
            phone = null;
        if (email is not null) emailOwners[email] = member.Id;
        if (phone is not null) phoneOwners[phone] = member.Id;

        member.Email = email;
        member.Phone = phone;
        if (IsMd5(source.PasswordHash) && (string.IsNullOrWhiteSpace(member.PasswordHash) || IsMd5(member.PasswordHash)))
            member.PasswordHash = source.PasswordHash.ToUpperInvariant();
        member.FirstName = source.FirstName;
        member.LastName = source.LastName;
        member.Gender = Key(source.Gender) switch
        {
            "ERKEK" => "male",
            "KADIN" or "KADİN" => "female",
            _ => member.Gender
        };
        member.BirthDate = ValidDate(source.BirthDate) is { } birth ? DateOnly.FromDateTime(birth) : member.BirthDate;
        if (cities.TryGetValue(Key(source.CityName), out var city)) member.CityId = city.Id;
        if (member.IdentityNumber is null && ValidTurkishIdentity(source.IdentityNumber))
            member.IdentityNumber = source.IdentityNumber.Trim();
        if (!LegacyConsentsMatch(member.Consents, source.EmailSubscribed, source.SmsSubscribed))
            member.Consents = MergeLegacyConsents(member.Consents, source.EmailSubscribed, source.SmsSubscribed);
        member.IsRegistered = true;
        member.IsEmailVerified = source.IsEmailVerified;
        member.IsPhoneVerified = source.IsPhoneVerified;
        member.IsActive = source.IsActive;
    }

    private static void ApplyAddress(
        Address address, LegacyAddressSourceRow source, Country? country,
        IReadOnlyDictionary<string, City> cities,
        IReadOnlyDictionary<string, District> districts,
        IReadOnlyDictionary<string, Neighborhood> neighborhoods)
    {
        cities.TryGetValue(Key(source.CityName), out var city);
        District? district = null;
        if (city is not null)
            districts.TryGetValue($"{city.Id:N}|{Key(source.DistrictName)}", out district);
        Neighborhood? neighborhood = null;
        if (district is not null)
            neighborhoods.TryGetValue($"{district.Id:N}|{Key(source.NeighborhoodName)}", out neighborhood);

        address.Title = string.IsNullOrWhiteSpace(source.Title) ? "Adres" : source.Title.Trim();
        address.CountryId = country?.Id;
        address.CountryName = string.IsNullOrWhiteSpace(source.CountryName)
            ? country is null ? "Türkiye" : TurkishName(country.NameI18n)
            : source.CountryName.Trim();
        address.CityId = city?.Id;
        address.CityName = source.CityName.Trim();
        address.DistrictId = district?.Id;
        address.DistrictName = source.DistrictName.Trim();
        address.NeighborhoodId = neighborhood?.Id;
        address.NeighborhoodName = NullIfEmpty(source.NeighborhoodName);
        address.AddressLine = NullIfEmpty(source.AddressLine);
        address.PostalCode = NullIfEmpty(source.PostalCode);
        address.RecipientName = $"{source.ContactFirstName} {source.ContactLastName}".Trim();
        address.RecipientPhone = source.ContactPhone.Trim();
    }

    private static Dictionary<string, object> MergeLegacyConsents(
        Dictionary<string, object>? current, bool email, bool sms)
    {
        var result = current is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(current);
        result["legacyMarketing"] = new Dictionary<string, bool>
        {
            ["email"] = email,
            ["sms"] = sms
        };
        return result;
    }

    private static bool LegacyConsentsMatch(Dictionary<string, object>? current, bool email, bool sms)
    {
        if (current is null || !current.TryGetValue("legacyMarketing", out var value)) return false;
        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
            return json.TryGetProperty("email", out var jsonEmail) && JsonBoolean(jsonEmail, out var e) && e == email
                && json.TryGetProperty("sms", out var jsonSms) && JsonBoolean(jsonSms, out var s) && s == sms;
        if (value is IReadOnlyDictionary<string, bool> booleans)
            return booleans.TryGetValue("email", out var e) && e == email
                && booleans.TryGetValue("sms", out var s) && s == sms;
        if (value is IReadOnlyDictionary<string, object> objects)
            return objects.TryGetValue("email", out var emailValue) && ObjectBoolean(emailValue, out var e) && e == email
                && objects.TryGetValue("sms", out var smsValue) && ObjectBoolean(smsValue, out var s) && s == sms;
        return false;
    }

    private static bool JsonBoolean(JsonElement value, out bool result)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result = value.GetBoolean();
            return true;
        }
        result = default;
        return false;
    }

    private static bool ObjectBoolean(object? value, out bool result)
    {
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }
        if (value is JsonElement json) return JsonBoolean(json, out result);
        result = default;
        return false;
    }

    private static int PendingEntityChanges(ICrmDbContext context)
    {
        if (context is not DbContext efContext)
            throw new InvalidOperationException("Legacy üye/adres importu EF DbContext gerektirir.");
        efContext.ChangeTracker.DetectChanges();
        return efContext.ChangeTracker.Entries()
            .Count(x => x.Entity is Member or Address
                && x.State is EntityState.Added or EntityState.Modified);
    }

    private static string AddressSignature(Address x) => string.Join('|',
        Key(x.Title), Key(x.AddressLine), Key(x.CityName), Key(x.DistrictName),
        Key(x.NeighborhoodName), NormalizePhone(x.RecipientPhone) ?? string.Empty, Key(x.RecipientName));

    private static string AddressSignature(LegacyAddressSourceRow x) => string.Join('|',
        Key(string.IsNullOrWhiteSpace(x.Title) ? "Adres" : x.Title), Key(x.AddressLine), Key(x.CityName),
        Key(x.DistrictName), Key(x.NeighborhoodName), NormalizePhone(x.ContactPhone) ?? string.Empty,
        Key($"{x.ContactFirstName} {x.ContactLastName}"));

    private static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    private static string? NormalizePhone(string? value)
    {
        var phone = value?.Trim();
        return string.IsNullOrWhiteSpace(phone) ? null : phone;
    }

    private static bool IsMd5(string? value) =>
        value is { Length: 32 } && value.All(Uri.IsHexDigit);

    private static DateTime? ValidDate(DateTime? value) =>
        value is { Year: >= 1900 } ? value : null;

    private DateTime? Utc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified), _sourceTimeZone)
    };

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch (Exception fallbackEx) when (fallbackEx is TimeZoneNotFoundException or InvalidTimeZoneException)
            { return TimeZoneInfo.Utc; }
        }
    }

    private static bool ValidTurkishIdentity(string? value)
    {
        if (value is null || value.Length != 11 || value[0] == '0' || value.Any(x => !char.IsDigit(x))) return false;
        var d = value.Select(x => x - '0').ToArray();
        var tenth = ((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10;
        if (tenth < 0) tenth += 10;
        return d[9] == tenth && d[10] == d.Take(10).Sum() % 10;
    }

    private static string TurkishName(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue("tr", out var tr) ? tr : values.Values.FirstOrDefault() ?? string.Empty;

    private static string Key(string? value) => (value ?? string.Empty).Trim().ToUpper(Turkish);
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
