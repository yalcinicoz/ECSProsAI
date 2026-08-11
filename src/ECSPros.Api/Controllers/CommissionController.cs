using ECSPros.Accounts.Application.Commands.PostAccountTransaction;
using ECSPros.Accounts.Application.Queries.GetSupplierSettlements;
using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Iam.Application.Services;
using ECSPros.Promotion.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// P3a (2026-08-11): Komisyon Yönetimi — TEK merkezi admin yüzeyi (kullanıcı kararı):
/// platform varsayılan grup oranları, satıcı sözleşmeleri (oran katmanları + ciro
/// basamakları + kargo modu), kampanya komisyon şartları ve hakediş/ödeme mutabakatı.
/// Kargo modu değişince satıcının ApiClient.FulfillmentMode'u senkronlanır (Yol B scope'u).
/// </summary>
[ApiController]
[Route("api/commission")]
[Authorize]
public class CommissionController(
    IMediator mediator,
    IAccountsDbContext accountsDb,
    IPromotionDbContext promotionDb,
    IIamDbContext iamDb) : ControllerBase
{
    // ── Platform varsayılan grup oranları ──
    [HttpGet("group-rates")]
    public async Task<IActionResult> GroupRates(CancellationToken ct)
    {
        var oranlar = await accountsDb.CommissionGroupRates.AsNoTracking()
            .Select(r => new { r.ProductGroupId, r.RatePercent })
            .ToListAsync(ct);
        return Ok(new { success = true, data = oranlar });
    }

    public record GroupRateItem(Guid ProductGroupId, decimal RatePercent);
    public record GroupRatesRequest(List<GroupRateItem> Items);

    /// <summary>Varsayılan oranları toplu upsert eder; listede olmayan mevcut kayıtlar SİLİNMEZ.</summary>
    [HttpPut("group-rates")]
    public async Task<IActionResult> UpdateGroupRates([FromBody] GroupRatesRequest request, CancellationToken ct)
    {
        if (request.Items.Any(i => i.RatePercent < 0 || i.RatePercent > 100))
            return BadRequest(new { success = false, error = "Oran %0-100 aralığında olmalıdır." });
        var idler = request.Items.Select(i => i.ProductGroupId).ToList();
        var mevcutlar = await accountsDb.CommissionGroupRates
            .Where(r => idler.Contains(r.ProductGroupId)).ToListAsync(ct);
        foreach (var item in request.Items)
        {
            var mevcut = mevcutlar.FirstOrDefault(r => r.ProductGroupId == item.ProductGroupId);
            if (mevcut is null)
                accountsDb.CommissionGroupRates.Add(new CommissionGroupRate
                { ProductGroupId = item.ProductGroupId, RatePercent = item.RatePercent });
            else
            { mevcut.RatePercent = item.RatePercent; mevcut.UpdatedAt = DateTime.UtcNow; }
        }
        await accountsDb.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // ── Satıcı sözleşmesi (oran katmanları + ciro basamakları + kargo modu) ──
    [HttpGet("suppliers/{accountId:guid}/contract")]
    public async Task<IActionResult> GetContract(Guid accountId, CancellationToken ct)
    {
        var contract = await accountsDb.SupplierContracts.AsNoTracking()
            .Include(c => c.GroupRates).Include(c => c.ProductRates).Include(c => c.TurnoverTiers)
            .FirstOrDefaultAsync(c => c.CurrentAccountId == accountId, ct);
        if (contract is null) return Ok(new { success = true, data = (object?)null });
        return Ok(new
        {
            success = true,
            data = new
            {
                contract.SettlementDelayDays,
                contract.PayoutPeriod,
                contract.CargoMode,
                contract.TurnoverPeriodType,
                contract.IsActive,
                contract.Notes,
                groupRates = contract.GroupRates.Select(r => new { r.ProductGroupId, r.RatePercent }),
                productRates = contract.ProductRates.Select(r => new { r.ProductId, r.RatePercent }),
                turnoverTiers = contract.TurnoverTiers.OrderBy(t => t.MinTurnover)
                    .Select(t => new { t.MinTurnover, t.RateAdjustmentPercent })
            }
        });
    }

    public record ContractGroupRate(Guid ProductGroupId, decimal RatePercent);
    public record ContractProductRate(Guid ProductId, decimal RatePercent);
    public record ContractTier(decimal MinTurnover, decimal RateAdjustmentPercent);
    public record ContractRequest(
        int SettlementDelayDays,
        string PayoutPeriod,
        string CargoMode,
        string TurnoverPeriodType,
        bool IsActive,
        string? Notes,
        List<ContractGroupRate>? GroupRates,
        List<ContractProductRate>? ProductRates,
        List<ContractTier>? TurnoverTiers);

    /// <summary>Sözleşmeyi bütün olarak upsert eder (oran listeleri TAM LİSTE — replace).</summary>
    [HttpPut("suppliers/{accountId:guid}/contract")]
    public async Task<IActionResult> UpsertContract(Guid accountId, [FromBody] ContractRequest request, CancellationToken ct)
    {
        if (request.SettlementDelayDays is < 0 or > 365)
            return BadRequest(new { success = false, error = "Hakediş gecikmesi 0-365 gün aralığında olmalıdır." });
        if (request.CargoMode is not ("platform_contract" or "seller_ships" or "seller_contract_we_ship"))
            return BadRequest(new { success = false, error = "Geçersiz kargo modu." });
        if (request.TurnoverPeriodType is not ("monthly" or "yearly" or "rolling12"))
            return BadRequest(new { success = false, error = "Geçersiz ciro dönemi." });
        if (request.PayoutPeriod is not ("weekly" or "monthly" or "immediate"))
            return BadRequest(new { success = false, error = "Geçersiz ödeme periyodu." });

        var hesapVar = await accountsDb.CurrentAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId, ct);
        if (!hesapVar) return NotFound(new { success = false, error = "Cari hesap bulunamadı." });

        var contract = await accountsDb.SupplierContracts
            .Include(c => c.GroupRates).Include(c => c.ProductRates).Include(c => c.TurnoverTiers)
            .FirstOrDefaultAsync(c => c.CurrentAccountId == accountId, ct);
        if (contract is null)
        {
            contract = new SupplierContract { CurrentAccountId = accountId };
            accountsDb.SupplierContracts.Add(contract);
        }
        contract.SettlementDelayDays = request.SettlementDelayDays;
        contract.PayoutPeriod = request.PayoutPeriod;
        contract.CargoMode = request.CargoMode;
        contract.TurnoverPeriodType = request.TurnoverPeriodType;
        contract.IsActive = request.IsActive;
        contract.Notes = request.Notes;
        contract.UpdatedAt = DateTime.UtcNow;

        contract.GroupRates.Clear();
        foreach (var r in request.GroupRates ?? [])
            contract.GroupRates.Add(new SupplierGroupRate { ProductGroupId = r.ProductGroupId, RatePercent = r.RatePercent });
        contract.ProductRates.Clear();
        foreach (var r in request.ProductRates ?? [])
            contract.ProductRates.Add(new SupplierProductRate { ProductId = r.ProductId, RatePercent = r.RatePercent });
        contract.TurnoverTiers.Clear();
        foreach (var t in request.TurnoverTiers ?? [])
            contract.TurnoverTiers.Add(new SupplierTurnoverTier { MinTurnover = t.MinTurnover, RateAdjustmentPercent = t.RateAdjustmentPercent });

        await accountsDb.SaveChangesAsync(ct);

        // K3 senkronu: kargo modu → satıcının API hesaplarının FulfillmentMode'u (Yol B scope türetimi)
        var hedefMod = request.CargoMode == "seller_ships" ? "supplier" : "platform";
        var apiClients = await iamDb.ApiClients
            .Where(c => c.OwnerType == "current_account" && c.OwnerId == accountId).ToListAsync(ct);
        foreach (var client in apiClients.Where(c => c.FulfillmentMode != hedefMod))
        { client.FulfillmentMode = hedefMod; client.UpdatedAt = DateTime.UtcNow; }
        if (apiClients.Count > 0) await iamDb.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }

    // ── Kampanya komisyon şartları (tek merkez kararı — kampanya formuna dokunulmaz) ──
    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var simdi = DateTime.UtcNow;
        var kampanyalar = await promotionDb.Campaigns.AsNoTracking()
            .Where(c => c.IsActive && (c.EndsAt == null || c.EndsAt >= simdi.AddDays(-30)))
            .OrderByDescending(c => c.StartsAt)
            .Select(c => new
            {
                c.Id, c.Code, c.NameI18n, c.StartsAt, c.EndsAt,
                c.SupplierCommissionRate, c.SupplierDiscountSharePercent, c.RequiresSupplierOptIn
            })
            .ToListAsync(ct);
        var katilimlar = await promotionDb.CampaignSupplierParticipations.AsNoTracking()
            .Where(p => p.IsActive).GroupBy(p => p.CampaignId)
            .Select(g => new { CampaignId = g.Key, Adet = g.Count() }).ToListAsync(ct);
        var katilimByKampanya = katilimlar.ToDictionary(k => k.CampaignId, k => k.Adet);
        return Ok(new
        {
            success = true,
            data = kampanyalar.Select(c => new
            {
                c.Id, c.Code, c.NameI18n, c.StartsAt, c.EndsAt,
                c.SupplierCommissionRate, c.SupplierDiscountSharePercent, c.RequiresSupplierOptIn,
                participantCount = katilimByKampanya.GetValueOrDefault(c.Id)
            })
        });
    }

    public record CampaignTermsRequest(decimal? SupplierCommissionRate, decimal SupplierDiscountSharePercent, bool RequiresSupplierOptIn);

    [HttpPut("campaigns/{id:guid}/supplier-terms")]
    public async Task<IActionResult> UpdateCampaignTerms(Guid id, [FromBody] CampaignTermsRequest request, CancellationToken ct)
    {
        if (request.SupplierDiscountSharePercent is < 0 or > 100
            || request.SupplierCommissionRate is < 0 or > 100)
            return BadRequest(new { success = false, error = "Oranlar %0-100 aralığında olmalıdır." });
        var kampanya = await promotionDb.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (kampanya is null) return NotFound(new { success = false, error = "Kampanya bulunamadı." });
        kampanya.SupplierCommissionRate = request.SupplierCommissionRate;
        kampanya.SupplierDiscountSharePercent = request.SupplierDiscountSharePercent;
        kampanya.RequiresSupplierOptIn = request.RequiresSupplierOptIn;
        kampanya.UpdatedAt = DateTime.UtcNow;
        await promotionDb.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // ── Hakediş mutabakatı ──
    [HttpGet("settlements")]
    public async Task<IActionResult> Settlements([FromQuery] Guid supplierAccountId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSupplierSettlementsQuery(supplierAccountId, status, null, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("suppliers/{accountId:guid}/statement")]
    public async Task<IActionResult> Statement(Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetSupplierStatementQuery(accountId, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ödeme çıkışı: satıcının TÜM 'available' satırlarını 'paid' işaretler ve toplam
    /// neti hakediş defterinden düşer (settlement_payout). Banka transferi manuel yapılır —
    /// bu kayıt mutabakat izidir.</summary>
    [HttpPost("suppliers/{accountId:guid}/payout")]
    public async Task<IActionResult> Payout(Guid accountId, CancellationToken ct)
    {
        var satirlar = await accountsDb.SettlementLines
            .Where(l => l.SupplierAccountId == accountId && l.Status == "available")
            .ToListAsync(ct);
        if (satirlar.Count == 0)
            return BadRequest(new { success = false, error = "Ödenecek 'available' hakediş satırı yok." });

        var toplam = satirlar.Sum(l => l.NetAmount);
        if (toplam <= 0)
            return BadRequest(new { success = false, error = $"Ödenecek net tutar pozitif değil ({toplam:0.00})." });

        var post = await mediator.Send(new PostAccountTransactionCommand(
            OwnerType: "external", OwnerId: accountId,
            ConceptCode: "hakedis", TransactionType: "settlement_payout",
            Debit: toplam, Credit: 0,
            ReferenceType: "payout", ReferenceId: null,
            Description: $"Hakediş ödemesi ({satirlar.Count} satır)",
            AccountId: accountId), ct);
        if (post.IsFailure) return BadRequest(new { success = false, error = post.Error });

        var simdi = DateTime.UtcNow;
        foreach (var satir in satirlar) { satir.Status = "paid"; satir.PaidAt = simdi; }
        await accountsDb.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { paidLines = satirlar.Count, totalNet = toplam } });
    }
}
