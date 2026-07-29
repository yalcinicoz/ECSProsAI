using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.AssignCargoCode;

/// <summary>Pakete kargo entegrasyon kodu atar (F3). ExternalCode verilirse pazaryeri/
/// taşıyıcı kodu aynen yazılır (source=external); verilmezse firma kargo entegrasyonunun
/// stratejisine göre üretilir (source=generated). Paketin mevcut kodu varsa eski kod
/// geçmişe yazılır; kargo süreci başlamış (gönderisi oluşmuş/etiketi basılmış) paketin
/// kodu değiştirilemez.</summary>
public record AssignCargoCodeCommand(
    Guid PackageId,
    Guid? FirmPlatformIntegrationId,
    string? ExternalCode,
    Guid ChangedBy,
    string? Reason = null) : IRequest<Result<string>>;
