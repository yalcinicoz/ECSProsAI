using ECSPros.Order.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Infrastructure.Services;

/// <summary>Core <c>core_payment_methods</c> tablosundan koda göre Id (raw SQL; FirmResolver kalıbı, İade planı §2.6).</summary>
public class PaymentMethodResolver(OrderDbContext db) : IPaymentMethodResolver
{
    private sealed class Row { public Guid Id { get; set; } }

    public async Task<Guid?> GetIdByCodeAsync(string code, CancellationToken ct = default)
    {
        var satirlar = await db.Database.SqlQuery<Row>($"""
            SELECT "Id" FROM core.core_payment_methods
            WHERE "Code" = {code} AND "IsDeleted" = false
            LIMIT 1
            """).ToListAsync(ct);
        return satirlar.Count == 0 ? null : satirlar[0].Id;
    }
}
