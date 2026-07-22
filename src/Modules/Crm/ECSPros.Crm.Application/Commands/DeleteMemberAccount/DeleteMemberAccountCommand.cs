using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.DeleteMemberAccount;

/// <summary>
/// B-019 (kabul testi 2026-07-22): üyenin KENDİ hesabını kapatması — soft delete
/// (IsDeleted, global query filter'la her sorgudan düşer: giriş yapılamaz, listelenmez)
/// + tüm oturumlar iptal. Sipariş/fatura kayıtları üyeden bağımsız yaşamaya devam eder
/// (ticari kayıt yükümlülüğü); kişisel görünürlük admin tarafında da soft-delete kuralına tabidir.
/// </summary>
public record DeleteMemberAccountCommand(Guid MemberId) : IRequest<Result<bool>>;

public class DeleteMemberAccountCommandHandler(ICrmDbContext db)
    : IRequestHandler<DeleteMemberAccountCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteMemberAccountCommand request, CancellationToken ct)
    {
        var uye = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (uye is null) return Result.Failure<bool>("Üye bulunamadı.");

        uye.IsDeleted = true;
        uye.DeletedAt = DateTime.UtcNow;
        uye.DeletedBy = request.MemberId; // kendi işlemi
        uye.IsActive = false;

        // Tüm oturumlar düşer — açık sekmelerdeki refresh token'lar da işe yaramaz
        var oturumlar = await db.MemberSessions
            .Where(s => s.MemberId == request.MemberId && s.IsActive)
            .ToListAsync(ct);
        foreach (var s in oturumlar) s.IsActive = false;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
