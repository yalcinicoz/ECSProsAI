using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Commands.AddRequestComment;

public record AddRequestCommentCommand(
    Guid RequestId,
    string? Comment,
    List<string>? Attachments,
    Guid UserId,
    string UserName) : IRequest<Result<Guid>>;

public class AddRequestCommentCommandHandler(IRequestsDbContext db)
    : IRequestHandler<AddRequestCommentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddRequestCommentCommand request, CancellationToken ct)
    {
        var ekler = request.Attachments ?? [];
        if (string.IsNullOrWhiteSpace(request.Comment) && ekler.Count == 0)
            return Result.Failure<Guid>("Yorum metni veya en az bir ek gönderilmelidir.");

        var talepVar = await db.ProjectRequests.AnyAsync(r => r.Id == request.RequestId, ct);
        if (!talepVar)
            return Result.Failure<Guid>("Talep bulunamadı.");

        var etkinlik = new RequestActivity
        {
            RequestId = request.RequestId,
            ActivityType = "comment",
            Comment = request.Comment?.Trim(),
            Attachments = ekler,
            UserId = request.UserId,
            UserName = request.UserName,
            CreatedBy = request.UserId,
        };
        db.RequestActivities.Add(etkinlik);
        await db.SaveChangesAsync(ct);
        return Result.Success(etkinlik.Id);
    }
}
