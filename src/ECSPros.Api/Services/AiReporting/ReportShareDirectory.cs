using ECSPros.Iam.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.AiReporting;

public sealed record ReportShareUser(bool IsActive, Dictionary<string, object>? Preferences);
public interface IReportShareDirectory
{
    Task<ReportShareUser?> FindAsync(Guid userId, CancellationToken ct);
}
public sealed class ReportShareDirectory(IIamDbContext db) : IReportShareDirectory
{
    public Task<ReportShareUser?> FindAsync(Guid userId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(u => u.Id == userId && !u.IsDeleted).Select(u => new ReportShareUser(u.IsActive, u.Preferences)).SingleOrDefaultAsync(ct);
}
