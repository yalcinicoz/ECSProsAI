using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.UpdateTransferStatus;

public record UpdateTransferStatusCommand(Guid Id, string Status, string? Notes) : IRequest<Result<bool>>;

public class UpdateTransferStatusCommandHandler : IRequestHandler<UpdateTransferStatusCommand, Result<bool>>
{
    private readonly IInventoryDbContext _db;

    private static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["draft"]       = ["pending", "cancelled"],
        ["pending"]     = ["picking", "cancelled"],
        ["picking"]     = ["picked", "cancelled"],
        ["picked"]      = ["in_transit"],
        ["in_transit"]  = ["delivered"],
        ["delivered"]   = ["completed"]
    };

    public UpdateTransferStatusCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateTransferStatusCommand request, CancellationToken ct)
    {
        var transfer = await _db.TransferRequests.FirstOrDefaultAsync(t => t.Id == request.Id, ct);
        if (transfer is null)
            return Result.Failure<bool>("Transfer bulunamadı.");

        if (!AllowedTransitions.TryGetValue(transfer.Status, out var allowed) || !allowed.Contains(request.Status))
            return Result.Failure<bool>($"'{transfer.Status}' durumundan '{request.Status}' durumuna geçiş yapılamaz.");

        transfer.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            transfer.Notes = request.Notes;
        transfer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
