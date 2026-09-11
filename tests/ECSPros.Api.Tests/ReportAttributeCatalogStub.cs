using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

internal sealed class ReportAttributeCatalogStub(params ReportField[] fields) : IReportAttributeCatalog
{
    public Task<IReadOnlyList<ReportField>> LoadAsync(IReadOnlySet<string> permissions, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReportField>>(fields);
}
