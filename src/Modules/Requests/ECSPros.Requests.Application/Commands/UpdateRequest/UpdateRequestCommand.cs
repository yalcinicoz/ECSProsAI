using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Commands.UpdateRequest;

public record UpdateRequestCommand(
    Guid RequestId,
    string Title,
    string Description,
    string Category,
    string Priority,
    DateOnly? DueDate,
    Guid UserId,
    string UserName) : IRequest<Result<bool>>;

public class UpdateRequestCommandHandler(IRequestsDbContext db)
    : IRequestHandler<UpdateRequestCommand, Result<bool>>
{
    private static readonly string[] Oncelikler = ["low", "normal", "high", "critical"];

    public async Task<Result<bool>> Handle(UpdateRequestCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<bool>("Talep başlığı zorunludur.");
        if (!CreateRequest.CreateRequestCommandHandler.Kategoriler.Contains(request.Category))
            return Result.Failure<bool>("Geçersiz kategori.");
        if (!Oncelikler.Contains(request.Priority))
            return Result.Failure<bool>("Geçersiz öncelik değeri.");

        var talep = await db.ProjectRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, ct);
        if (talep is null)
            return Result.Failure<bool>("Talep bulunamadı.");
        if (talep.Status is "done" or "rejected" or "cancelled")
            return Result.Failure<bool>("Kapanmış talep düzenlenemez.");

        talep.Title = request.Title.Trim();
        talep.Description = request.Description.Trim();
        talep.Category = request.Category;
        talep.Priority = request.Priority;
        talep.DueDate = request.DueDate;
        talep.UpdatedBy = request.UserId;

        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = talep.Id,
            ActivityType = "updated",
            UserId = request.UserId,
            UserName = request.UserName,
            CreatedBy = request.UserId,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
