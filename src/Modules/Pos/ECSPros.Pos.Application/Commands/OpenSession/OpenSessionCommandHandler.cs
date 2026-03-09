using ECSPros.Pos.Application.Services;
using ECSPros.Pos.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Commands.OpenSession;

public class OpenSessionCommandHandler : IRequestHandler<OpenSessionCommand, Result<Guid>>
{
    private readonly IPosDbContext _context;

    public OpenSessionCommandHandler(IPosDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(OpenSessionCommand request, CancellationToken cancellationToken)
    {
        var registerExists = await _context.PosRegisters.AnyAsync(r => r.Id == request.RegisterId, cancellationToken);
        if (!registerExists)
            return Result.Failure<Guid>("Kasa bulunamadı.");

        var openSession = await _context.PosSessions.AnyAsync(
            s => s.RegisterId == request.RegisterId && s.Status == "open",
            cancellationToken);
        if (openSession)
            return Result.Failure<Guid>("Bu kasada zaten açık bir oturum var.");

        var sessionNumber = $"POS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var session = new PosSession
        {
            RegisterId = request.RegisterId,
            UserId = request.UserId,
            SessionNumber = sessionNumber,
            OpenedAt = DateTime.UtcNow,
            OpeningCash = request.OpeningCash,
            Status = "open",
            Notes = request.Notes
        };

        _context.PosSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(session.Id);
    }
}
