using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.CreateMember;

public class CreateMemberCommandHandler : IRequestHandler<CreateMemberCommand, Result<Guid>>
{
    private readonly ICrmDbContext _context;
    private readonly IMemberPasswordHasher _passwordHasher;

    public CreateMemberCommandHandler(ICrmDbContext context, IMemberPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(CreateMemberCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await _context.Members.AnyAsync(
                m => m.Email == request.Email && !m.IsDeleted, cancellationToken);
            if (emailExists)
                return Result.Failure<Guid>("Bu e-posta adresi zaten kayıtlı.");
        }

        var groupExists = await _context.MemberGroups.AnyAsync(g => g.Id == request.MemberGroupId, cancellationToken);
        if (!groupExists)
            return Result.Failure<Guid>("Üye grubu bulunamadı.");

        var member = new Member
        {
            MemberGroupId = request.MemberGroupId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Gender = request.Gender,
            BirthDate = request.BirthDate,
            CompanyName = request.CompanyName,
            TaxNumber = request.TaxNumber,
            TaxOffice = request.TaxOffice,
            IsRegistered = !string.IsNullOrWhiteSpace(request.Email) || !string.IsNullOrWhiteSpace(request.Phone),
            IsActive = true
        };

        if (!string.IsNullOrWhiteSpace(request.Password))
            member.PasswordHash = _passwordHasher.Hash(request.Password); // D5: BCrypt

        _context.Members.Add(member);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(member.Id);
    }
}
