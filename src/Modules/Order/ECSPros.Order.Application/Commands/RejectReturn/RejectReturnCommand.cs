using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.RejectReturn;

public record RejectReturnCommand(Guid ReturnId, string Reason) : IRequest<Result<bool>>;

public class RejectReturnCommandHandler : IRequestHandler<RejectReturnCommand, Result<bool>>
{
    private readonly IOrderDbContext _db;

    public RejectReturnCommandHandler(IOrderDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RejectReturnCommand request, CancellationToken ct)
    {
        var ret = await _db.Returns.FirstOrDefaultAsync(r => r.Id == request.ReturnId, ct);
        if (ret is null)
            return Result.Failure<bool>("İade bulunamadı.");

        if (ret.Status != "requested")
            return Result.Failure<bool>($"İade yalnızca 'requested' durumunda reddedilebilir. Mevcut durum: {ret.Status}");

        ret.Status = "rejected";
        ret.InspectionNotes = request.Reason;
        ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
