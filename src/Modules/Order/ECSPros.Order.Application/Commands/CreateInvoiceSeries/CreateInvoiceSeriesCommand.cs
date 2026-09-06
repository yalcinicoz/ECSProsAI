using System.Text.RegularExpressions;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateInvoiceSeries;

/// <summary>FE0: tekil, TİPLİ fatura serisi tanımı. Aynı harfler firma içinde tipten bağımsız bir kez.</summary>
public record CreateInvoiceSeriesCommand(
    Guid FirmId,
    string Serial,
    string InvoiceType,
    string? Name,
    string? Description,
    Guid? IntegrationContractId,
    Guid CreatedBy) : IRequest<Result<Guid>>;

public partial class CreateInvoiceSeriesCommandHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<CreateInvoiceSeriesCommand, Result<Guid>>
{
    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex SerialRegex();

    public async Task<Result<Guid>> Handle(CreateInvoiceSeriesCommand request, CancellationToken ct)
    {
        var serial = (request.Serial ?? "").Trim().ToUpperInvariant();
        if (!SerialRegex().IsMatch(serial))
            return Result.Failure<Guid>("Seri 3 büyük harften oluşmalıdır (ör. MSH).");
        if (!InvoiceTypes.IsValid(request.InvoiceType))
            return Result.Failure<Guid>("Seri tipi e_archive, e_invoice veya export olmalıdır.");

        var clash = await db.InvoiceSeries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.FirmId == request.FirmId && s.Serial == serial, ct);
        if (clash is not null)
            return Result.Failure<Guid>(
                $"{serial} serisi bu firmada zaten {InvoiceTypes.Label(clash.InvoiceType)} tipiyle tanımlı — aynı harfler ikinci bir tipte kullanılamaz.");

        var contractError = await InvoiceSeriesContractRules.ValidateAsync(firmResolver, request.FirmId, request.IntegrationContractId, ct);
        if (contractError is not null) return Result.Failure<Guid>(contractError);

        var series = new InvoiceSeries
        {
            FirmId = request.FirmId,
            Serial = serial,
            InvoiceType = request.InvoiceType,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IntegrationContractId = request.IntegrationContractId,
            IsActive = true,
            CreatedBy = request.CreatedBy
        };
        db.InvoiceSeries.Add(series);
        await db.SaveChangesAsync(ct);
        return Result.Success(series.Id);
    }
}

/// <summary>Sözleşme doğrulaması: verildiyse firmaya ait, einvoice tipli ve firma-geneli olmalı.
/// FE3 ile ZORUNLU (2026-09-06) — bkz. plan §2.2.</summary>
public static class InvoiceSeriesContractRules
{
    public static async Task<string?> ValidateAsync(IFirmResolver firmResolver, Guid firmId, Guid? contractId, CancellationToken ct)
    {
        // FE3 (2026-09-06): sözleşme ZORUNLU — her seri bir entegratör sözleşmesine aittir (plan §0.2)
        if (contractId is null)
            return "Her seri bir entegratör sözleşmesine bağlanmalıdır — önce Ayarlar → Firmalar → Entegrasyonlar'dan e-fatura entegratörü sözleşmesi tanımlayın.";
        var contracts = await firmResolver.GetEInvoiceContractsAsync(firmId, ct);
        var contract = contracts.FirstOrDefault(c => c.Id == contractId.Value);
        if (contract is null) return "Entegratör sözleşmesi bulunamadı ya da bu firmaya ait değil.";
        if (contract.FirmPlatformId is not null) return "Entegratör sözleşmesi firma geneli olmalıdır (kanala özel sözleşme seriye bağlanamaz).";
        if (!contract.IsActive) return "Pasif entegratör sözleşmesi seriye bağlanamaz.";
        return null;
    }
}
