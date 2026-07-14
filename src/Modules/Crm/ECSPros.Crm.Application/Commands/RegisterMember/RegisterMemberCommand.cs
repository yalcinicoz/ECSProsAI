using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.RegisterMember;

public record RegisterMemberCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone = null,
    List<MemberConsent>? Consents = null,
    string? Gender = null,
    DateOnly? BirthDate = null) : IRequest<Result<Guid>>;

/// <summary>D3: kayıtta onaylanan belge kaydı — Member.Consents jsonb'sine
/// "acceptedContracts" anahtarıyla yazılır. ContentUpdatedAt, onay anındaki metin
/// sürümünü sabitler (CMS'te metin sonradan değişse de kanıt kalır).</summary>
public record MemberConsent(
    [property: System.Text.Json.Serialization.JsonPropertyName("code")] string Code,
    [property: System.Text.Json.Serialization.JsonPropertyName("title")] string Title,
    [property: System.Text.Json.Serialization.JsonPropertyName("acceptedAt")] DateTime AcceptedAt,
    [property: System.Text.Json.Serialization.JsonPropertyName("contentUpdatedAt")] DateTime? ContentUpdatedAt);

public class RegisterMemberCommandHandler(ICrmDbContext db, IMemberPasswordHasher passwordHasher)
    : IRequestHandler<RegisterMemberCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterMemberCommand request, CancellationToken ct)
    {
        var exists = await db.Members
            .AnyAsync(m => m.Email == request.Email.ToLowerInvariant(), ct);
        if (exists)
            return Result.Failure<Guid>("Bu e-posta adresi zaten kayıtlı.");

        var defaultGroup = await db.MemberGroups
            .FirstOrDefaultAsync(g => g.IsDefault && !g.IsDeleted, ct);
        if (defaultGroup is null)
            return Result.Failure<Guid>("Varsayılan üye grubu bulunamadı.");

        var member = new Member
        {
            MemberGroupId = defaultGroup.Id,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = passwordHasher.Hash(request.Password), // D5: BCrypt
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Gender = request.Gender,
            BirthDate = request.BirthDate,
            IsRegistered = true,
            IsActive = true,
            Consents = request.Consents is { Count: > 0 }
                ? new Dictionary<string, object> { ["acceptedContracts"] = request.Consents }
                : null
        };

        db.Members.Add(member);
        await db.SaveChangesAsync(ct);
        return Result.Success(member.Id);
    }
}
