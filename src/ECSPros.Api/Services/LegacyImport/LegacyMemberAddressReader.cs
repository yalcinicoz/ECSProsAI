using System.Data.Common;
using MySql.Data.MySqlClient;

namespace ECSPros.Api.Services.LegacyImport;

public sealed record LegacyMemberSourceRow(
    int Id,
    string IdentityNumber,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    string PasswordHash,
    DateTime? BirthDate,
    string Gender,
    string CityName,
    bool IsActive,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    bool EmailSubscribed,
    bool SmsSubscribed,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record LegacyAddressSourceRow(
    int Id,
    int MemberId,
    string Title,
    string AddressLine,
    string PostalCode,
    string NeighborhoodName,
    string DistrictName,
    string CityName,
    string CountryName,
    string ContactFirstName,
    string ContactLastName,
    string ContactPhone,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record LegacyMemberAddressSnapshot(
    IReadOnlyList<LegacyMemberSourceRow> Members,
    IReadOnlyList<LegacyAddressSourceRow> Addresses);

public interface ILegacyMemberAddressReader
{
    Task<LegacyMemberAddressSnapshot> ReadAsync(int platformId, CancellationToken ct);
}

public sealed class LegacyMemberAddressReader(ILegacyReadSource source) : ILegacyMemberAddressReader
{
    public Task<LegacyMemberAddressSnapshot> ReadAsync(int platformId, CancellationToken ct) =>
        source.ExecuteReadAsync(async (connection, transaction, token) =>
        {
            var members = await ReadMembersAsync(connection, transaction, platformId, token);
            var addresses = await ReadAddressesAsync(connection, transaction, platformId, token);
            return new LegacyMemberAddressSnapshot(members, addresses);
        }, ct);

    private static async Task<IReadOnlyList<LegacyMemberSourceRow>> ReadMembersAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, tcKimlikNo, firstName, lastName, phone, email, password,
                   CASE WHEN birthDate IS NULL OR YEAR(birthDate)=0 THEN NULL ELSE birthDate END,
                   gender, cityName, isActive, epostaOnayli, telefonOnayli, emailSubscribed, smsSubscribed,
                   CASE WHEN createdDate IS NULL OR YEAR(createdDate)=0 THEN NULL ELSE createdDate END,
                   CASE WHEN updatedDate IS NULL OR YEAR(updatedDate)=0 THEN NULL ELSE updatedDate END
              FROM webmembers
             WHERE platformId = @platformId
             ORDER BY Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyMemberSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Text(reader, 6), Date(reader, 7),
                Text(reader, 8), Text(reader, 9), Bool(reader, 10), Bool(reader, 11),
                Bool(reader, 12), Bool(reader, 13), Bool(reader, 14), Date(reader, 15), Date(reader, 16)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<LegacyAddressSourceRow>> ReadAddressesAsync(
        MySqlConnection connection, MySqlTransaction transaction, int platformId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id, memberId, addressTitle, addressDetail, postalCode, neighborhoodName,
                   districtName, cityName, countryName, contactFirstName, contactLastName, contactPhone,
                   CASE WHEN createdDate IS NULL OR YEAR(createdDate)=0 THEN NULL ELSE createdDate END,
                   CASE WHEN updatedDate IS NULL OR YEAR(updatedDate)=0 THEN NULL ELSE updatedDate END
              FROM webmemberaddresses
             WHERE platformId = @platformId
             ORDER BY memberId, Id
            """;
        command.Parameters.AddWithValue("@platformId", platformId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<LegacyAddressSourceRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new(
                reader.GetInt32(0), reader.GetInt32(1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8),
                Text(reader, 9), Text(reader, 10), Text(reader, 11), Date(reader, 12), Date(reader, 13)));
        }
        return rows;
    }

    private static string Text(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;

    private static DateTime? Date(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static bool Bool(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToInt64(reader.GetValue(ordinal)) != 0;
}
