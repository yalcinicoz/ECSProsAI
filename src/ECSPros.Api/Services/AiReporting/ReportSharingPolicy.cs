using System.Security.Cryptography;
using System.Text;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

public static class ReportSharingPolicy
{
    // Eligibility only: the controller separately revalidates both users' effective
    // group permissions (including user revocations) against every recipe field.
    public static bool ActiveParticipants(bool ownerActive, bool viewerActive) => ownerActive && viewerActive;
    public static bool CanShare(EfektifYetkiler effective) => effective.Var(ReportDictionary.SharePermission)
        && effective.Kanallar(ReportDictionary.SharePermission) is null
        && ReportDictionary.CanReport(StockReportExecutor.ResolvePermissions(effective));
    public static bool TokenMatches(string? stored, string? provided) => stored?.Length == 64 && provided?.Length == 64
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided));
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
