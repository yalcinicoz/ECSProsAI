using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetSupplierUser;

/// <summary>Tek panel kullanıcısı — /api/supplier/auth/me introspection'ı için.</summary>
public record GetSupplierUserQuery(Guid Id) : IRequest<Result<SupplierUserMeDto>>;

public record SupplierUserMeDto(
    Guid Id, Guid CurrentAccountId, string Email, string FullName,
    bool IsActive, DateTime? LastLoginAt);

public class GetSupplierUserQueryHandler(IIamDbContext db)
    : IRequestHandler<GetSupplierUserQuery, Result<SupplierUserMeDto>>
{
    public async Task<Result<SupplierUserMeDto>> Handle(GetSupplierUserQuery request, CancellationToken ct)
    {
        var u = await db.SupplierUsers.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (u is null) return Result.Failure<SupplierUserMeDto>("Kullanıcı bulunamadı.");
        return Result.Success(new SupplierUserMeDto(
            u.Id, u.CurrentAccountId, u.Email, u.FullName, u.IsActive, u.LastLoginAt));
    }
}
