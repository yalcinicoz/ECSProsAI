using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.ProductQuestions;

/// <summary>Satıcıya Soru Sor (2026-09-01): üye ürün detayından soru sorar — pending doğar,
/// admin cevabıyla yayına girer. Spam freni: üyenin aynı üründe CEVAPSIZ sorusu varken
/// yenisi engellenir. MemberName maskeli anlık görüntü (API katmanı üretir).</summary>
public record CreateProductQuestionCommand(
    Guid FirmPlatformId, Guid MemberId, string ProductCode, string Question, string MaskedMemberName)
    : IRequest<Result<Guid>>;

public class CreateProductQuestionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<CreateProductQuestionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductQuestionCommand request, CancellationToken ct)
    {
        var soru = request.Question?.Trim() ?? "";
        if (soru.Length < 10)
            return Result.Failure<Guid>("Sorunuz en az 10 karakter olmalıdır.");
        if (soru.Length > 1000)
            return Result.Failure<Guid>("Sorunuz en fazla 1000 karakter olabilir.");

        var bekleyenVar = await db.ProductQuestions.AnyAsync(q =>
            q.FirmPlatformId == request.FirmPlatformId && q.MemberId == request.MemberId
            && q.ProductCode == request.ProductCode && q.Status == "pending", ct);
        if (bekleyenVar)
            return Result.Failure<Guid>("Bu ürün için cevap bekleyen bir sorunuz zaten var.");

        var kayit = new ProductQuestion
        {
            FirmPlatformId = request.FirmPlatformId,
            MemberId = request.MemberId,
            ProductCode = request.ProductCode.Trim(),
            Question = soru,
            MemberName = request.MaskedMemberName,
            Status = "pending",
        };
        db.ProductQuestions.Add(kayit);
        await db.SaveChangesAsync(ct);
        return Result.Success(kayit.Id);
    }
}

/// <summary>Admin cevabı: answer dolu → answered (yayında). Yayındaki cevap güncellenebilir.</summary>
public record AnswerProductQuestionCommand(Guid QuestionId, string Answer, Guid? AnsweredBy)
    : IRequest<Result<bool>>;

public class AnswerProductQuestionCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<AnswerProductQuestionCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(AnswerProductQuestionCommand request, CancellationToken ct)
    {
        var cevap = request.Answer?.Trim() ?? "";
        if (cevap.Length < 2) return Result.Failure<bool>("Cevap boş olamaz.");
        if (cevap.Length > 2000) return Result.Failure<bool>("Cevap en fazla 2000 karakter olabilir.");

        var soru = await db.ProductQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);
        if (soru is null) return Result.Failure<bool>("Soru bulunamadı.");

        soru.Answer = cevap;
        soru.Status = "answered";
        soru.AnsweredAt = DateTime.UtcNow;
        soru.AnsweredBy = request.AnsweredBy;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Yayından kaldır (hidden) ya da geri yayınla (answered — yalnız cevaplıysa).</summary>
public record SetProductQuestionVisibilityCommand(Guid QuestionId, bool Hidden) : IRequest<Result<bool>>;

public class SetProductQuestionVisibilityCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<SetProductQuestionVisibilityCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetProductQuestionVisibilityCommand request, CancellationToken ct)
    {
        var soru = await db.ProductQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);
        if (soru is null) return Result.Failure<bool>("Soru bulunamadı.");
        if (request.Hidden) soru.Status = "hidden";
        else if (soru.Answer is { Length: > 0 }) soru.Status = "answered";
        else return Result.Failure<bool>("Cevapsız soru yayına alınamaz — önce cevaplayın.");
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
