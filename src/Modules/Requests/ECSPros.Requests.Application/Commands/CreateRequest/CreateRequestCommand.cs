using ECSPros.Requests.Application.Services;
using ECSPros.Requests.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Commands.CreateRequest;

public record CreateRequestCommand(
    string Title,
    string Description,
    string Category,
    string Priority,
    DateOnly? DueDate,
    List<string>? Attachments,
    Guid UserId,
    string UserName) : IRequest<Result<Guid>>;

public class CreateRequestCommandHandler(IRequestsDbContext db)
    : IRequestHandler<CreateRequestCommand, Result<Guid>>
{
    private static readonly string[] Oncelikler = ["low", "normal", "high", "critical"];
    // V1 sabit set (2026-07-23): core lookup değerlerinde Code kolonu yok, kod bazlı
    // kategori lookup'a bağlanamıyor — ihtiyaç büyürse lookup entegrasyonu ayrı iş.
    public static readonly string[] Kategoriler = ["yeni_ozellik", "hata", "iyilestirme", "veri_isi", "diger"];

    public async Task<Result<Guid>> Handle(CreateRequestCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<Guid>("Talep başlığı zorunludur.");
        if (!Kategoriler.Contains(request.Category))
            return Result.Failure<Guid>("Geçersiz kategori.");
        if (!Oncelikler.Contains(request.Priority))
            return Result.Failure<Guid>("Geçersiz öncelik değeri.");

        // Okunur talep no: TLP-2026-0001 — yıl içi sıra. Soft delete edilenler de sayılır
        // (IgnoreQueryFilters), aksi halde numara tekrarlanır. Düşük hacimde yarış riski ihmal edilir.
        var yil = DateTime.UtcNow.Year;
        var onek = $"TLP-{yil}-";
        var yilIciSayi = await db.ProjectRequests.IgnoreQueryFilters()
            .CountAsync(r => r.Code.StartsWith(onek), ct);
        var talep = new ProjectRequest
        {
            Code = $"{onek}{yilIciSayi + 1:0000}",
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Priority = request.Priority,
            Status = "new",
            RequestedBy = request.UserId,
            RequestedByName = request.UserName,
            DueDate = request.DueDate,
            CreatedBy = request.UserId,
        };
        db.ProjectRequests.Add(talep);
        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = talep.Id,
            ActivityType = "created",
            UserId = request.UserId,
            UserName = request.UserName,
            Attachments = request.Attachments ?? [],
            CreatedBy = request.UserId,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(talep.Id);
    }
}
