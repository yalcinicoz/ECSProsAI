using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.RejectProductSubmission;

/// <summary>Kapı 2 — insan reddi: pending gönderimi gerekçeyle reddeder (canlıya çıkmaz).</summary>
public record RejectProductSubmissionCommand(Guid SubmissionId, string Reason, Guid? ReviewedBy)
    : IRequest<Result<bool>>;

public class RejectProductSubmissionCommandHandler : IRequestHandler<RejectProductSubmissionCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;

    public RejectProductSubmissionCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RejectProductSubmissionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<bool>("Red gerekçesi zorunludur.");

        var submission = await _db.ProductSubmissions.FirstOrDefaultAsync(s => s.Id == request.SubmissionId, ct);
        if (submission is null)
            return Result.Failure<bool>("Gönderim bulunamadı.");
        if (submission.Status != "pending")
            return Result.Failure<bool>($"Gönderim '{submission.Status}' durumunda; yalnız pending reddedilir.");

        submission.Status = "rejected";
        submission.ReviewNote = request.Reason.Trim();
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedBy = request.ReviewedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
