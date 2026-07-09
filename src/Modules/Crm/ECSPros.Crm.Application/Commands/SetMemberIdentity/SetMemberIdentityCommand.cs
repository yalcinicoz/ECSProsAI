using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.SetMemberIdentity;

/// <summary>
/// C7 (K9): TCKN kaydı — format + kontrol basamağı algoritması sunucuda doğrulanır
/// (11 hane, ilk hane 0 değil; 10. hane = ((tek hanelerin toplamı×7) − çift hanelerin
/// toplamı) mod 10; 11. hane = ilk 10 hanenin toplamı mod 10). NVİ/KPS resmi doğrulaması
/// ileride — o güne dek IdentityVerifiedAt yalnız algoritma doğrulamasını temsil eder.
/// Doğum tarihi verilirse üye profiline yazılır (boşsa dokunulmaz).
/// </summary>
public record SetMemberIdentityCommand(
    Guid MemberId,
    string IdentityNumber,
    DateOnly? BirthDate = null) : IRequest<Result<bool>>;

public static class TcknValidator
{
    public static bool Gecerli(string? tckn)
    {
        if (string.IsNullOrWhiteSpace(tckn)) return false;
        var t = tckn.Trim();
        if (t.Length != 11 || t[0] == '0' || !t.All(char.IsDigit)) return false;

        var d = t.Select(c => c - '0').ToArray();
        var tekler = d[0] + d[2] + d[4] + d[6] + d[8];
        var ciftler = d[1] + d[3] + d[5] + d[7];
        var hane10 = ((tekler * 7) - ciftler) % 10;
        if (hane10 < 0) hane10 += 10;
        var hane11 = d.Take(10).Sum() % 10;
        return d[9] == hane10 && d[10] == hane11;
    }
}

public class SetMemberIdentityCommandHandler(ICrmDbContext db)
    : IRequestHandler<SetMemberIdentityCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetMemberIdentityCommand request, CancellationToken ct)
    {
        if (!TcknValidator.Gecerli(request.IdentityNumber))
            return Result.Failure<bool>("Geçersiz T.C. kimlik numarası.");

        var uye = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (uye is null)
            return Result.Failure<bool>("Üye bulunamadı.");

        uye.IdentityNumber = request.IdentityNumber.Trim();
        uye.IdentityVerifiedAt = DateTime.UtcNow;
        if (request.BirthDate.HasValue && uye.BirthDate is null)
            uye.BirthDate = request.BirthDate;
        uye.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
