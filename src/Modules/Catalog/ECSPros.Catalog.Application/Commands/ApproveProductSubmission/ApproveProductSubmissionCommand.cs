using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Catalog.Application.Commands.ApproveProductSubmission;

/// <summary>Kapı 2 — insan onayı: pending gönderimi canlı Product'a dönüştürür (grup, varyantlar,
/// eksen değerleri, ürün özellikleri, görseller, SupplierId). Değer adları havuz Id'lerine çözülür.
/// (§3.8). Yalnız YENİ ürün oluşturur; aynı (SupplierId, kod) için canlı ürün varsa red döner
/// (revizyon onayı sonraki dilim).</summary>
public record ApproveProductSubmissionCommand(Guid SubmissionId, Guid? ReviewedBy)
    : IRequest<Result<ApproveProductSubmissionResult>>;

public record ApproveProductSubmissionResult(Guid ProductId, string ProductCode);
