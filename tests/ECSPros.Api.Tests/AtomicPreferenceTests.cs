using System.Data.Common;
using ECSPros.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECSPros.Api.Tests;

[TestClass]
public class AtomicPreferenceTests
{
    private sealed class Connection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken ct = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    private sealed class Capture : DbCommandInterceptor
    {
        public string Sql = "";
        public object[] Values = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Sql = command.CommandText;
            Values = command.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
    [TestMethod]
    public async Task PreferenceWritesUseParameterizedSingleKeySql_WithoutConnection()
    {
        var capture = new Capture();
        using var db = new IamDbContext(new DbContextOptionsBuilder<IamDbContext>()
            .UseNpgsql("Host=localhost;Database=not_opened;Username=unused")
            .AddInterceptors(new Connection(), capture).Options);
        var id = Guid.NewGuid();
        const string value = "{\"name\":\"private-test-name\"}";
        Assert.AreEqual(1, await db.WritePreferenceAsync(id, "reports.ai.saved.1", value, true, default));
        StringAssert.Contains(capture.Sql, "jsonb_set");
        StringAssert.Contains(capture.Sql, "ARRAY[");
        StringAssert.Contains(capture.Sql, "NOT \"IsDeleted\"");
        Assert.IsFalse(capture.Sql.Contains("private-test-name"));
        Assert.IsFalse(capture.Sql.Contains(id.ToString()));
        CollectionAssert.Contains(capture.Values, value);
        CollectionAssert.Contains(capture.Values, id);
        CollectionAssert.Contains(capture.Values, true);
        await db.WritePreferenceAsync(id, "grids.orders", null, false, default);
        CollectionAssert.Contains(capture.Values, false);
        StringAssert.Contains(capture.Sql, "IS NULL");
        await db.CompareExchangePreferenceAsync(id, "reports.ai.saved.1", value, null, default);
        StringAssert.Contains(capture.Sql, "\"Preferences\" ->");
        StringAssert.Contains(capture.Sql, "::jsonb");
        Assert.IsFalse(capture.Sql.Contains("private-test-name"));
        CollectionAssert.Contains(capture.Values, value);
        CollectionAssert.Contains(capture.Values, id);
    }
}
