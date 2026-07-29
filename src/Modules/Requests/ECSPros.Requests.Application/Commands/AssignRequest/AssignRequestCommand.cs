using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Commands.AssignRequest;

/// <summary>AssignedTo null → atama kaldırılır.</summary>
public record AssignRequestCommand(
    Guid RequestId,
    Guid? AssignedTo,
    string? AssignedToName,
    Guid UserId,
    string UserName) : IRequest<Result<bool>>;

public class AssignRequestCommandHandler(IRequestsDbContext db)
    : IRequestHandler<AssignRequestCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AssignRequestCommand request, CancellationToken ct)
    {
        var talep = await db.ProjectRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);
        if (talep is null)
            return Result.Failure<bool>("Talep bulunamadı.");
        if (talep.Status is "done" or "rejected" or "cancelled")
            return Result.Failure<bool>("Kapanmış talebe atama yapılamaz.");
        if (request.AssignedTo is not null && string.IsNullOrWhiteSpace(request.AssignedToName))
            return Result.Failure<bool>("Atanan kişinin adı gönderilmelidir.");

        var eskiAd = talep.AssignedToName;
        talep.AssignedTo = request.AssignedTo;
        talep.AssignedToName = request.AssignedTo is null ? null : request.AssignedToName;
        talep.UpdatedBy = request.UserId;

        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = talep.Id,
            ActivityType = "assignment",
            OldValue = eskiAd,
            NewValue = talep.AssignedToName,
            UserId = request.UserId,
            UserName = request.UserName,
            CreatedBy = request.UserId,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
