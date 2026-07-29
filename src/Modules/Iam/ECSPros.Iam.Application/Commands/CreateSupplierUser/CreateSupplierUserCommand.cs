using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Commands.CreateSupplierUser;

/// <summary>Bir cari karta (pazaryeri satıcısı) panel kullanıcısı açar. Cari kartın gerçekten
/// AccountType=supplier + SupplierKind=marketplace + aktif olması controller'da (Accounts sorgusu)
/// doğrulanır — IAM modülü Accounts'a bağımlı olmasın diye burada YALNIZ e-posta tekilliği kontrol edilir.</summary>
public record CreateSupplierUserCommand(
    Guid CurrentAccountId, string Email, string Password, string FullName)
    : IRequest<Result<Guid>>;

public class CreateSupplierUserCommandHandler(IIamDbContext db, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateSupplierUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSupplierUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result.Failure<Guid>("Geçerli bir e-posta gerekli.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Result.Failure<Guid>("Şifre en az 6 karakter olmalı.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Result.Failure<Guid>("Ad Soyad gerekli.");

        if (await db.SupplierUsers.AnyAsync(u => u.Email == email, ct))
            return Result.Failure<Guid>($"'{email}' e-postası zaten kullanımda.");

        var user = new SupplierUser
        {
            CurrentAccountId = request.CurrentAccountId,
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            IsActive = true,
        };
        db.SupplierUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return Result.Success(user.Id);
    }
}
