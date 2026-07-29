using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Commands.ChangeRequestStatus;

public record ChangeRequestStatusCommand(
    Guid RequestId,
    string NewStatus,
    string? Comment,
    Guid UserId,
    string UserName) : IRequest<Result<bool>>;

public class ChangeRequestStatusCommandHandler(IRequestsDbContext db)
    : IRequestHandler<ChangeRequestStatusCommand, Result<bool>>
{
    /// <summary>Kurgu kararı 2026-07-23: tam akış — testten geriye (in_progress) dönüş var,
    /// terminal durumlardan (done/rejected/cancelled) çıkış yok.</summary>
    private static readonly Dictionary<string, string[]> GecisHaritasi = new()
    {
        ["new"] = ["evaluation", "rejected", "cancelled"],
        ["evaluation"] = ["planned", "rejected", "cancelled"],
        ["planned"] = ["in_progress", "cancelled"],
        ["in_progress"] = ["testing", "cancelled"],
        ["testing"] = ["done", "in_progress", "cancelled"],
    };

    public async Task<Result<bool>> Handle(ChangeRequestStatusCommand request, CancellationToken ct)
    {
        var talep = await db.ProjectRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);
        if (talep is null)
            return Result.Failure<bool>("Talep bulunamadı.");

        if (!GecisHaritasi.TryGetValue(talep.Status, out var izinliler))
            return Result.Failure<bool>($"'{talep.Status}' durumundaki talep artık değiştirilemez.");
        if (!izinliler.Contains(request.NewStatus))
            return Result.Failure<bool>($"'{talep.Status}' durumundan '{request.NewStatus}' durumuna geçilemez.");

        var eski = talep.Status;
        talep.Status = request.NewStatus;
        talep.UpdatedBy = request.UserId;
        talep.CompletedAt = request.NewStatus is "done" or "rejected" or "cancelled"
            ? DateTime.UtcNow
            : null;

        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = talep.Id,
            ActivityType = "status_change",
            OldValue = eski,
            NewValue = request.NewStatus,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            UserId = request.UserId,
            UserName = request.UserName,
            CreatedBy = request.UserId,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
