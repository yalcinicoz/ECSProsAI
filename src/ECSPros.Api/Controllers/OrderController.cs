using ECSPros.Order.Application.Commands.AddOrderPayment;
using ECSPros.Order.Application.Commands.ApproveReturn;
using ECSPros.Order.Application.Commands.CancelInvoice;
using ECSPros.Order.Application.Commands.CancelOrder;
using ECSPros.Order.Application.Commands.CompleteRefund;
using ECSPros.Order.Application.Commands.ConfirmOrder;
using ECSPros.Order.Application.Commands.ConvertQuoteToOrder;
using ECSPros.Order.Application.Commands.CreateGiftCard;
using ECSPros.Order.Application.Commands.CreateInvoice;
using ECSPros.Order.Application.Commands.ManageOrderNumberSeries;
using ECSPros.Order.Application.Commands.UpdateInvoiceSeries;
using ECSPros.Order.Application.Commands.RegisterExternalInvoice;
using ECSPros.Order.Application.Queries.GetInvoiceSeriesGaps;
using ECSPros.Order.Application.Queries.GetInvoiceDispatches;
using ECSPros.Order.Application.Queries.GetEInvoiceContracts;
using ECSPros.Order.Application.Commands.RetryInvoiceDispatch;
using ECSPros.Order.Application.Commands.ResendInvoice;
using ECSPros.Order.Application.Commands.DeactivateInvoiceSeries;
using ECSPros.Order.Application.Commands.ActivateInvoiceSeries;
using ECSPros.Order.Application.Commands.SetChannelInvoiceSettings;
using ECSPros.Order.Application.Queries.GetChannelInvoiceSettings;
using ECSPros.Order.Application.Commands.CreateOrder;
using ECSPros.Order.Application.Commands.CreateQuote;
using ECSPros.Order.Application.Commands.CreateReturn;
using ECSPros.Order.Application.Commands.MarkDelivered;
using ECSPros.Order.Application.Commands.MarkShipped;
using ECSPros.Order.Application.Commands.ReceiveReturn;
using ECSPros.Order.Application.Commands.RejectReturn;
using ECSPros.Order.Application.Commands.RespondQuote;
using ECSPros.Order.Application.Commands.SendQuote;
using ECSPros.Order.Application.Commands.StartProcessing;
using ECSPros.Order.Application.Commands.UseGiftCard;
using ECSPros.Order.Application.Queries.GetGiftCardBalance;
using ECSPros.Order.Application.Queries.GetGiftCards;
using ECSPros.Order.Application.Queries.GetInvoices;
using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrderPayments;
using ECSPros.Order.Application.Queries.GetOrderShipments;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Order.Application.Queries.GetOrderStatusCounts;
using ECSPros.Order.Application.Queries.GetInvoiceSeries;
using ECSPros.Order.Application.Commands.CreateInvoiceSeries;
using ECSPros.Order.Application.Commands.SetInvoiceIntegratorUrl;
using ECSPros.Order.Application.Queries.GetQuotes;
using ECSPros.Order.Application.Queries.GetReturnDetail;
using ECSPros.Order.Application.Queries.GetReturns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Orders ───────────────────────────────────────────────────────────────

    /// <summary>Siparişleri sayfalı listeler. `statuses` virgüllü çoklu durum; `to` exclusive üst sınır.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status, [FromQuery] string? statuses, [FromQuery] Guid? memberId,
        [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] bool? paymentCollected = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var statusList = SplitCsv(statuses);
        // DataGrid F0 (2026-09-08): sort/dir + f.* filtreleri (OrderGrid.Schema beyaz listesi); page/pageSize merkezi clamp (1..250).
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, defaultPageSize: 20);
        var result = await _mediator.Send(new GetOrdersQuery(
            status, memberId, search, grid.Page, grid.PageSize,
            statusList, AsUtc(from), AsUtc(to), firmPlatformId, paymentMethod, paymentCollected, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Durum bazlı sipariş sayıları — yalnız aktif durumlar sayılır (kapalı durumlar milyonlara ulaşır).
    /// Listeyle aynı filtre parametrelerini (search, from/to, paymentMethod, paymentCollected, f.*) alır; durum filtresi hariç uygulanır.</summary>
    [HttpGet("status-counts")]
    public async Task<IActionResult> GetOrderStatusCounts(
        [FromQuery] string? statuses, [FromQuery] string? search = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] Guid? memberId = null, [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? paymentMethod = null, [FromQuery] bool? paymentCollected = null, CancellationToken ct = default)
    {
        var statusList = SplitCsv(statuses) ?? new List<string> { "pending", "confirmed", "processing", "shipped" };
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query);
        var filters = new OrderListFilters(null, null, memberId, firmPlatformId, AsUtc(from), AsUtc(to), paymentMethod, paymentCollected, search);
        var result = await _mediator.Send(new GetOrderStatusCountsQuery(statusList, filters, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// Siparişleri Excel'e aktarır (DataGrid F0/F3, plan §2.8): gövdede aynı filtre modeli (search/sort/dir/filters + named:
    /// statuses, status, from, to, memberId, firmPlatformId, paymentMethod, paymentCollected) + kolon listesi (boş = tümü).
    /// Sayfalama uygulanmaz; tavan Grid:ExportMaxRows (100.000), kullanıcı bazlı dakikada Grid:ExportPerMinute (5).
    /// </summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportOrders([FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<OrderController> logger, CancellationToken ct)
    {
        var grid = body.ToGridRequest();
        DateTime? Tarih(string key) => DateTime.TryParse(body.NamedValue(key), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
        Guid? Kimlik(string key) => Guid.TryParse(body.NamedValue(key), out var g) ? g : null;
        bool? Bayrak(string key) => bool.TryParse(body.NamedValue(key), out var b) ? b : null;
        var filters = new OrderListFilters(body.NamedValue("status"), SplitCsv(body.NamedValue("statuses")), Kimlik("memberId"), Kimlik("firmPlatformId"),
            Tarih("from"), Tarih("to"), body.NamedValue("paymentMethod"), Bayrak("paymentCollected"), grid.Search);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var src = await _mediator.Send(new ExportOrdersQuery(filters, grid, config.GetValue("Grid:ExportMaxRows", 100_000)), ct);
        if (src.IsFailure) return BadRequest(new { success = false, error = src.Error });
        var cols = ECSPros.Api.Grid.GridExportWriter.Select(ECSPros.Api.Grid.OrderExportColumns.All, body.Columns);
        var file = await ECSPros.Api.Grid.GridExportWriter.WriteToTempAsync(src.Value!.Rows.AsEnumerable(), cols, "Siparişler", ct);
        await ECSPros.Api.Grid.GridExportWriter.AuditAsync(iam, HttpContext, "orders", src.Value.Count,
            new { grid.Search, grid.Sort, grid.Dir, filters = grid.Filters.Select(f => $"{f.Field} {f.Op} {f.Value}").ToList(), named = body.Named, columns = cols.Select(c => c.Key).ToList() },
            sw, logger, ct);
        return File(file, ECSPros.Api.Grid.GridExportWriter.XlsxMime, ECSPros.Api.Grid.GridExportWriter.FileName("siparisler"));
    }

    private static List<string>? SplitCsv(string? csv) => string.IsNullOrWhiteSpace(csv) ? null
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // timestamptz kolonlara Kind=Unspecified DateTime yazılamaz (Npgsql)
    private static DateTime? AsUtc(DateTime? d) => d is null ? null
        : d.Value.Kind == DateTimeKind.Utc ? d : d.Value.ToUniversalTime();

    /// <summary>Sipariş detayını döner.</summary>
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetail(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni sipariş oluşturur.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new OrderItemDto(i.VariantId, i.Quantity, i.UnitPrice, i.UnitType)).ToList();
        var result = await _mediator.Send(new CreateOrderCommand(
            request.FirmPlatformId, request.MemberId, request.OrderType, request.PaymentMethod,
            request.CurrencyCode, request.ShippingRecipientName, request.ShippingRecipientPhone,
            request.ShippingCountryId, request.ShippingCityId, request.ShippingDistrictId,
            request.ShippingAddressLine, request.ShippingPostalCode, items, request.CustomerNotes,
            request.ExternalOrderNumber), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/{result.Value}", new { success = true, data = new { orderNumber = result.Value } });
    }

    /// <summary>Siparişi onaylar ve stok rezervasyonu oluşturur.</summary>
    [HttpPost("{orderId:guid}/confirm")]
    public async Task<IActionResult> ConfirmOrder(Guid orderId, [FromBody] ConfirmOrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ConfirmOrderCommand(orderId, request.WarehouseId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi iptal eder ve stok rezervasyonlarını serbest bırakır.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CancelOrderCommand(orderId, uid, request.Reason), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi işleme alır (picking planı atanabilir).</summary>
    [HttpPost("{orderId:guid}/start-processing")]
    public async Task<IActionResult> StartProcessing(Guid orderId, [FromBody] StartProcessingRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new StartProcessingCommand(orderId, request.PickingPlanId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi kargoya verir, Shipment kaydı oluşturur, stok rezervasyonunu tüketir.</summary>
    [HttpPost("{orderId:guid}/ship")]
    public async Task<IActionResult> MarkShipped(Guid orderId, [FromBody] MarkShippedRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new MarkShippedCommand(orderId, request.FirmIntegrationId, request.TrackingNumber, request.PackageCount, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { shipmentId = result.Value } });
    }

    /// <summary>Siparişi teslim edildi olarak işaretler.</summary>
    [HttpPost("{orderId:guid}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid orderId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new MarkDeliveredCommand(orderId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Returns ──────────────────────────────────────────────────────────────

    /// <summary>İade taleplerini listeler.</summary>
    [HttpGet("returns")]
    public async Task<IActionResult> GetReturns(
        [FromQuery] Guid? orderId, [FromQuery] Guid? memberId, [FromQuery] string? status, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // DataGrid F4: page/pageSize/search/sort/dir/f.* (ReturnGrid.Schema beyaz listesi)
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, defaultPageSize: 20);
        var result = await _mediator.Send(new GetReturnsQuery(orderId, memberId, status, grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>İadeleri Excel'e aktarır (DataGrid F4): gövde search/sort/dir/filters/columns + named: status, orderId, memberId.</summary>
    [HttpPost("returns/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportReturns([FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<OrderController> logger, CancellationToken ct)
    {
        var filters = new ReturnListFilters(ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "orderId"),
            ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "memberId"), body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "returns", "iadeler", "İadeler",
            ECSPros.Api.Grid.ReturnExportColumns.All, max => _mediator.Send(new ExportReturnsQuery(filters, body.ToGridRequest(), max), ct), ct);
    }

    /// <summary>İade talebi detayını döner.</summary>
    [HttpGet("returns/{returnId:guid}")]
    public async Task<IActionResult> GetReturnDetail(Guid returnId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReturnDetailQuery(returnId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Müşteri iade talebi oluşturur.</summary>
    [HttpPost("{orderId:guid}/returns")]
    public async Task<IActionResult> CreateReturn(Guid orderId, [FromBody] CreateReturnRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateReturnCommand(
            orderId, request.MemberId, request.ReturnType, request.CustomerNotes, request.RefundMethod,
            request.Items.Select(i => new ReturnItemRequest(i.OrderItemId, i.VariantId, i.Quantity, i.ReturnReasonId, i.CustomerNotes)).ToList()), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/returns/{result.Value}", new { success = true, data = new { returnId = result.Value } });
    }

    /// <summary>İade talebini onaylar.</summary>
    [HttpPost("returns/{returnId:guid}/approve")]
    public async Task<IActionResult> ApproveReturn(Guid returnId, [FromServices] ECSPros.Api.Services.Push.PushEtkilesim push, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ApproveReturnCommand(returnId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await push.IadeDurumuAsync(returnId, "İade talebiniz onaylandı", "approved", ct);
        return Ok(new { success = true });
    }

    /// <summary>İadeyi reddeder (requested → rejected).</summary>
    [HttpPatch("returns/{returnId:guid}/reject")]
    public async Task<IActionResult> RejectReturn(Guid returnId, [FromBody] RejectReturnRequest request, [FromServices] ECSPros.Api.Services.Push.PushEtkilesim push, CancellationToken ct)
    {
        var result = await _mediator.Send(new RejectReturnCommand(returnId, request.Reason), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await push.IadeDurumuAsync(returnId, "İade talebiniz reddedildi", "rejected", ct);
        return Ok(new { success = true });
    }

    /// <summary>İade kargosu depoda teslim alındı — stok otomatik geri yüklenir.</summary>
    [HttpPost("returns/{returnId:guid}/receive")]
    public async Task<IActionResult> ReceiveReturn(Guid returnId, [FromBody] ReceiveReturnRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ReceiveReturnCommand(returnId, request.WarehouseId, request.InspectionNotes, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>İade bedelini öder — iade tamamlanır.</summary>
    [HttpPost("returns/{returnId:guid}/refund")]
    public async Task<IActionResult> CompleteRefund(Guid returnId, [FromBody] CompleteRefundRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CompleteRefundCommand(returnId, request.RefundMethod, request.Amount, uid, request.Details), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Invoices ─────────────────────────────────────────────────────────────

    /// <summary>Fatura listesi.</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? orderId, [FromQuery] string? status, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // DataGrid F4: page/pageSize/search/sort/dir/f.* (InvoiceGrid.Schema beyaz listesi)
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, defaultPageSize: 20);
        var result = await _mediator.Send(new GetInvoicesQuery(orderId, status, grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Faturaları Excel'e aktarır (DataGrid F4): gövde search/sort/dir/filters/columns + named: status, orderId.</summary>
    [HttpPost("invoices/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportInvoices([FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<OrderController> logger, CancellationToken ct)
    {
        var filters = new InvoiceListFilters(ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "orderId"), body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "invoices", "faturalar", "Faturalar",
            ECSPros.Api.Grid.InvoiceExportColumns.All, max => _mediator.Send(new ExportInvoicesQuery(filters, body.ToGridRequest(), max), ct), ct);
    }

    /// <summary>Sipariş için fatura oluşturur.</summary>
    [HttpPost("{orderId:guid}/invoices")]
    public async Task<IActionResult> CreateInvoice(Guid orderId, [FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateInvoiceCommand(
            orderId, request.InvoiceSeriesId, request.InvoiceType, request.InvoiceDate,
            request.RecipientName, request.RecipientAddress,
            request.RecipientTaxOffice, request.RecipientTaxNumber, request.RecipientCompanyName, uid,
            request.PackageId), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/invoices/{result.Value}", new { success = true, data = new { invoiceId = result.Value } });
    }

    // ── Sipariş Numarası Serileri (F5 — kanal başına seri tanımı) ──────────────

    /// <summary>Kanal başına sipariş numarası serilerini listeler (serisiz kanallar da görünür).</summary>
    [HttpGet("number-series")]
    public async Task<IActionResult> GetOrderNumberSeries(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderNumberSeriesQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kanalın sipariş numarası serisini tanımlar/günceller —
    /// sayaç (NextValue) elle değiştirilemez, numaralar havuza geri dönmez.</summary>
    [HttpPut("number-series/{firmPlatformId:guid}")]
    public async Task<IActionResult> UpsertOrderNumberSeries(
        Guid firmPlatformId, [FromBody] UpsertNumberSeriesRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpsertOrderNumberSeriesCommand(
            firmPlatformId, request.Prefix, request.PadLength, request.IsActive), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Fatura serileri — FE0: tekil, TİPLİ (firma/tip süzgeci; kanal yuvası ve fatura formu seçicileri).</summary>
    [HttpGet("invoice-series")]
    public async Task<IActionResult> GetInvoiceSeries(
        [FromQuery] bool activeOnly = true, [FromQuery] Guid? firmId = null, [FromQuery] string? invoiceType = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetInvoiceSeriesQuery(activeOnly, firmId, invoiceType), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni fatura serisi tanımlar (FE0: serial + tip zorunlu; aynı harfler firma içinde bir kez).</summary>
    [HttpPost("invoice-series")]
    public async Task<IActionResult> CreateInvoiceSeries([FromBody] CreateInvoiceSeriesRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateInvoiceSeriesCommand(
            request.FirmId, request.Serial, request.InvoiceType, request.Name, request.Description,
            request.IntegrationContractId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created("/api/orders/invoice-series", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>FE1: dış numaralı (ERP / pazaryeri / entegratör) fatura kaydı — idempotent (kaynak, numara).</summary>
    [HttpPost("{orderId:guid}/invoices/external")]
    public async Task<IActionResult> RegisterExternalInvoice(Guid orderId, [FromBody] RegisterExternalInvoiceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new RegisterExternalInvoiceCommand(
            orderId, request.PackageId, request.NumberSource, request.ExternalSource, request.InvoiceNumber,
            request.InvoiceType, request.InvoiceDate, request.Ettn, request.ExternalDocumentId,
            request.RecipientName, request.RecipientAddress, request.RecipientTaxOffice, request.RecipientTaxNumber,
            request.RecipientCompanyName, request.IntegratorInvoiceUrl, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        var data = new { id = result.Value!.InvoiceId, alreadyExisted = result.Value.AlreadyExisted };
        return result.Value.AlreadyExisted ? Ok(new { success = true, data }) : Created($"/api/orders/invoices/{data.id}", new { success = true, data });
    }

    /// <summary>FE3: firma bazlı e-fatura entegratör sözleşmeleri (seri formu seçicisi; kimlik değeri dönmez).</summary>
    [HttpGet("invoice-series/contracts")]
    public async Task<IActionResult> GetEInvoiceContracts([FromQuery] Guid? firmId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetEInvoiceContractsQuery(firmId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>FE4: gönderim kuyruğu (status: pending|done|dead|blocked; invoiceId ile fatura zaman çizelgesi).</summary>
    [HttpGet("invoice-dispatches")]
    public async Task<IActionResult> GetInvoiceDispatches([FromQuery] string? status, [FromQuery] Guid? invoiceId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetInvoiceDispatchesQuery(status, invoiceId, page, Math.Clamp(pageSize, 1, 200)), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>FE4: ölü/engelli gönderim işini yeniden kuyruğa alır.</summary>
    [HttpPost("invoice-dispatches/{id:guid}/retry")]
    public async Task<IActionResult> RetryInvoiceDispatch(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new RetryInvoiceDispatchCommand(id, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>FE4: fatura için yeni gönderim işi açar (bekleyen iş yoksa).</summary>
    [HttpPost("invoices/{invoiceId:guid}/resend")]
    public async Task<IActionResult> ResendInvoice(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new ResendInvoiceCommand(invoiceId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { dispatchId = result.Value } });
    }

    /// <summary>FE1: seri boşluk denetimi (sayaç ↔ kayıtlı numaralar).</summary>
    [HttpGet("invoice-series/{id:guid}/gaps")]
    public async Task<IActionResult> GetInvoiceSeriesGaps(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceSeriesGapsQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Seri ad/açıklama/sözleşme günceller (serial ve tip değişmez).</summary>
    [HttpPut("invoice-series/{id:guid}")]
    public async Task<IActionResult> UpdateInvoiceSeries(Guid id, [FromBody] UpdateInvoiceSeriesRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new UpdateInvoiceSeriesCommand(id, request.Name, request.Description, request.IntegrationContractId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Seriyi pasife alır; kanal bağı varsa yerine geçecek (aynı tip) seri zorunlu, bağlar taşınır.</summary>
    [HttpPost("invoice-series/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateInvoiceSeries(Guid id, [FromBody] DeactivateInvoiceSeriesRequest? request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new DeactivateInvoiceSeriesCommand(id, request?.ReplacementSeriesId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("invoice-series/{id:guid}/activate")]
    public async Task<IActionResult> ActivateInvoiceSeries(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new ActivateInvoiceSeriesCommand(id, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kanal faturalama ayarları: gönderim yöntemi + e-arşiv/e-fatura/ihracat yuvaları + uyarılar (FE0 §2.3).</summary>
    [HttpGet("invoice-settings/channels")]
    public async Task<IActionResult> GetChannelInvoiceSettings(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetChannelInvoiceSettingsQuery(), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("invoice-settings/channels/{firmPlatformId:guid}")]
    public async Task<IActionResult> GetChannelInvoiceSettingsOne(Guid firmPlatformId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetChannelInvoiceSettingsQuery(firmPlatformId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value!.First() });
    }

    /// <summary>Kanal faturalama ayarını yazar — yuva tipi ile seri tipi uyuşmazsa 400.</summary>
    [HttpPut("invoice-settings/channels/{firmPlatformId:guid}")]
    public async Task<IActionResult> SetChannelInvoiceSettings(Guid firmPlatformId, [FromBody] SetChannelInvoiceSettingsRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });
        var result = await _mediator.Send(new SetChannelInvoiceSettingsCommand(
            firmPlatformId, request.SendMethod ?? "manual", request.EArchiveSeriesId, request.EInvoiceSeriesId, request.ExportSeriesId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Entegratör PDF adresini kaydeder (P1d — storefront "Faturayı Görüntüle" kaynağı).</summary>
    [HttpPatch("invoices/{invoiceId:guid}/integrator-url")]
    public async Task<IActionResult> SetInvoiceIntegratorUrl(Guid invoiceId, [FromBody] SetInvoiceIntegratorUrlRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new SetInvoiceIntegratorUrlCommand(invoiceId, request.IntegratorInvoiceUrl, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Fatura iptali.</summary>
    [HttpPost("invoices/{invoiceId:guid}/cancel")]
    public async Task<IActionResult> CancelInvoice(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CancelInvoiceCommand(invoiceId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Quotes ───────────────────────────────────────────────────────────────

    /// <summary>Teklif listesi (DataGrid: page/pageSize/search/sort/dir/f.* — QuoteGrid.Schema; named: memberId, status).</summary>
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] Guid? memberId, [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct = default)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, defaultPageSize: 20);
        var result = await _mediator.Send(new GetQuotesQuery(memberId, status, grid.Page, grid.PageSize, search, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Teklifleri Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: memberId, status.</summary>
    [HttpPost("quotes/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportQuotes([FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<OrderController> logger, CancellationToken ct)
    {
        var filters = new QuoteListFilters(ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "memberId"), body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "quotes", "teklifler", "Teklifler",
            ECSPros.Api.Grid.QuoteExportColumns.All, max => _mediator.Send(new ExportQuotesQuery(filters, body.ToGridRequest(), max), ct), ct);
    }

    /// <summary>Yeni teklif oluşturur.</summary>
    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote([FromBody] CreateQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateQuoteCommand(
            request.FirmPlatformId, request.MemberId, request.CurrencyCode, request.ValidUntil,
            request.NotesToCustomer, request.InternalNotes,
            request.Items.Select(i => new QuoteItemRequest(
                i.VariantId, i.Sku, i.ProductName, i.VariantInfo, i.Quantity,
                i.UnitType, i.UnitPrice, i.DiscountRate, i.TaxRate, i.Notes)).ToList(), uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/quotes/{result.Value}", new { success = true, data = new { quoteId = result.Value } });
    }

    /// <summary>Teklifi müşteriye gönderir.</summary>
    [HttpPost("quotes/{quoteId:guid}/send")]
    public async Task<IActionResult> SendQuote(Guid quoteId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new SendQuoteCommand(quoteId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Teklifi kabul veya reddeder.</summary>
    [HttpPost("quotes/{quoteId:guid}/respond")]
    public async Task<IActionResult> RespondQuote(Guid quoteId, [FromBody] RespondQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new RespondQuoteCommand(quoteId, request.Accepted, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kabul edilen teklifi siparişe dönüştürür.</summary>
    [HttpPost("quotes/{quoteId:guid}/convert")]
    public async Task<IActionResult> ConvertQuoteToOrder(Guid quoteId, [FromBody] ConvertQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ConvertQuoteToOrderCommand(
            quoteId, request.ShippingRecipientName, request.ShippingRecipientPhone,
            request.ShippingCountryId, request.ShippingCityId, request.ShippingDistrictId,
            request.ShippingAddressLine, request.ShippingPostalCode, request.PaymentMethodId, uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/{result.Value}", new { success = true, data = new { orderId = result.Value } });
    }

    // ─── Shipments ────────────────────────────────────────────────────────────

    /// <summary>Sipariş kargo takip bilgileri.</summary>
    [HttpGet("{id:guid}/shipments")]
    public async Task<IActionResult> GetOrderShipments(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderShipmentsQuery(id), ct);
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Payments ─────────────────────────────────────────────────────────────

    /// <summary>Sipariş ödemelerini listeler.</summary>
    [HttpGet("{id:guid}/payments")]
    public async Task<IActionResult> GetOrderPayments(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderPaymentsQuery(id), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Siparişe ödeme ekler.</summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddOrderPayment(Guid id, [FromBody] AddOrderPaymentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddOrderPaymentCommand(id, request.PaymentMethodId, request.Amount, request.CurrencyCode), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/{id}/payments", new { success = true, data = new { id = result.Value } });
    }

    // ─── Gift Cards ───────────────────────────────────────────────────────────

    /// <summary>Hediye kartlarını sayfalı listeler (panel; DataGrid: search/sort/dir/f.* — GiftCardGrid.Schema; named: status).</summary>
    [HttpGet("gift-cards")]
    public async Task<IActionResult> GetGiftCards(
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct = default)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, defaultPageSize: 20);
        var result = await _mediator.Send(new GetGiftCardsQuery(status, search, grid.Page, grid.PageSize, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Hediye kartlarını Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: status.</summary>
    [HttpPost("gift-cards/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportGiftCards([FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<OrderController> logger, CancellationToken ct)
    {
        var filters = new GiftCardListFilters(body.NamedValue("status"), body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "gift-cards", "hediye-kartlari", "Hediye Kartları",
            ECSPros.Api.Grid.GiftCardExportColumns.All, max => _mediator.Send(new ExportGiftCardsQuery(filters, body.ToGridRequest(), max), ct), ct);
    }

    /// <summary>Hediye kartı bakiyesi sorgular.</summary>
    [HttpGet("gift-cards/{code}")]
    public async Task<IActionResult> GetGiftCardBalance(string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGiftCardBalanceQuery(code), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni hediye kartı oluşturur.</summary>
    [HttpPost("gift-cards")]
    public async Task<IActionResult> CreateGiftCard([FromBody] CreateGiftCardRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateGiftCardCommand(
            request.FirmId, request.Amount, request.CurrencyCode,
            request.ValidFrom, request.ValidUntil, request.IsSingleUse,
            request.CreatedForMemberId, request.CreatedFromOrderId, uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/gift-cards/{result.Value}", new { success = true, data = new { giftCardId = result.Value } });
    }

    /// <summary>Hediye kartını siparişe uygular.</summary>
    [HttpPost("gift-cards/use")]
    public async Task<IActionResult> UseGiftCard([FromBody] UseGiftCardRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new UseGiftCardCommand(request.Code, request.Amount, request.OrderId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { deductedAmount = result.Value } });
    }
}

// ─── Request records ──────────────────────────────────────────────────────────

public record CreateOrderRequest(
    Guid FirmPlatformId, Guid MemberId, string OrderType, string PaymentMethod,
    string CurrencyCode, string ShippingRecipientName, string ShippingRecipientPhone,
    Guid ShippingCountryId, Guid ShippingCityId, Guid ShippingDistrictId,
    string ShippingAddressLine, string? ShippingPostalCode,
    List<OrderItemRequest> Items, string? CustomerNotes = null,
    string? ExternalOrderNumber = null);

public record OrderItemRequest(Guid VariantId, int Quantity, decimal UnitPrice, string? UnitType = null);
public record ConfirmOrderRequest(Guid WarehouseId);
public record CancelOrderRequest(string? Reason = null);
public record StartProcessingRequest(Guid? PickingPlanId = null);
public record MarkShippedRequest(Guid? FirmIntegrationId, string? TrackingNumber, int PackageCount = 1);

public record CreateReturnItemRequest(
    Guid OrderItemId, Guid VariantId, int Quantity, Guid ReturnReasonId, string? CustomerNotes);

public record CreateReturnRequest(
    Guid MemberId, string ReturnType, string? CustomerNotes,
    string RefundMethod, List<CreateReturnItemRequest> Items);

public record ReceiveReturnRequest(Guid WarehouseId, string? InspectionNotes);

public record UpsertNumberSeriesRequest(string Prefix, int PadLength, bool IsActive = true);

public record CreateInvoiceSeriesRequest(
    Guid FirmId, string Serial, string InvoiceType, string? Name = null, string? Description = null,
    Guid? IntegrationContractId = null);

public record UpdateInvoiceSeriesRequest(string? Name, string? Description, Guid? IntegrationContractId);
public record RegisterExternalInvoiceRequest(
    string NumberSource, string ExternalSource, string InvoiceNumber, string InvoiceType, DateTime InvoiceDate,
    Guid? PackageId = null, Guid? Ettn = null, string? ExternalDocumentId = null,
    string? RecipientName = null, string? RecipientAddress = null, string? RecipientTaxOffice = null,
    string? RecipientTaxNumber = null, string? RecipientCompanyName = null, string? IntegratorInvoiceUrl = null);
public record DeactivateInvoiceSeriesRequest(Guid? ReplacementSeriesId);
public record SetChannelInvoiceSettingsRequest(
    string? SendMethod, Guid? EArchiveSeriesId, Guid? EInvoiceSeriesId, Guid? ExportSeriesId);

public record SetInvoiceIntegratorUrlRequest(string? IntegratorInvoiceUrl);

public record CompleteRefundRequest(
    string RefundMethod, decimal Amount, Dictionary<string, object>? Details = null);

public record CreateInvoiceRequest(
    Guid InvoiceSeriesId, string InvoiceType, DateTime InvoiceDate,
    string RecipientName, string RecipientAddress,
    string? RecipientTaxOffice, string? RecipientTaxNumber, string? RecipientCompanyName,
    Guid? PackageId = null);

public record QuoteItemHttpRequest(
    Guid VariantId, string Sku, string ProductName, string VariantInfo,
    int Quantity, string UnitType, decimal UnitPrice,
    decimal DiscountRate, decimal TaxRate, string? Notes);

public record CreateQuoteRequest(
    Guid FirmPlatformId, Guid MemberId, string CurrencyCode, DateTime ValidUntil,
    string? NotesToCustomer, string? InternalNotes, List<QuoteItemHttpRequest> Items);

public record RespondQuoteRequest(bool Accepted);

public record ConvertQuoteRequest(
    string ShippingRecipientName, string ShippingRecipientPhone,
    Guid ShippingCountryId, Guid ShippingCityId, Guid ShippingDistrictId,
    string ShippingAddressLine, string? ShippingPostalCode, Guid PaymentMethodId);

public record CreateGiftCardRequest(
    Guid FirmId, decimal Amount, string CurrencyCode,
    DateOnly ValidFrom, DateOnly? ValidUntil, bool IsSingleUse,
    Guid? CreatedForMemberId, Guid? CreatedFromOrderId);

public record UseGiftCardRequest(string Code, decimal Amount, Guid OrderId);
public record AddOrderPaymentRequest(Guid PaymentMethodId, decimal Amount, string CurrencyCode = "TRY");
public record RejectReturnRequest(string Reason);
