using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetSupplierUsers;

public record SupplierUserDto(
    Guid Id, Guid CurrentAccountId, string Email, string FullName,
    bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt);

/// <summary>Bir cari karta bağlı panel kullanıcıları (admin "API/Kullanıcılar" ekranı + test).</summary>
public record GetSupplierUsersQuery(Guid CurrentAccountId) : IRequest<Result<List<SupplierUserDto>>>;

public class GetSupplierUsersQueryHandler(IIamDbContext db)
    : IRequestHandler<GetSupplierUsersQuery, Result<List<SupplierUserDto>>>
{
    public async Task<Result<List<SupplierUserDto>>> Handle(GetSupplierUsersQuery request, CancellationToken ct)
    {
        var items = await db.SupplierUsers
            .Where(u => u.CurrentAccountId == request.CurrentAccountId)
            .OrderBy(u => u.FullName)
            .Select(u => new SupplierUserDto(
                u.Id, u.CurrentAccountId, u.Email, u.FullName, u.IsActive, u.LastLoginAt, u.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(items);
    }
}
