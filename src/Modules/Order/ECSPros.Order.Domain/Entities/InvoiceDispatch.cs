using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

/// <summary>
/// FE4 gönderim outbox'ı (plan §2.6): entegratöre gönderilecek/iptal edilecek fatura işi. Fatura kesildiğinde
/// (kanal yöntemi integrator_api) "send", iptalde "cancel" kaydı düşer. Worker üstel geri çekilmeyle dener;
/// azami denemede "dead" (ölü mektup — panelden Tekrar Dene), adaptör/sözleşme yoksa "blocked".
/// </summary>
public class InvoiceDispatch : BaseEntity
{
    public Guid InvoiceId { get; set; }
    /// <summary>send | cancel | status</summary>
    public string Action { get; set; } = InvoiceDispatchActions.Send;
    /// <summary>pending | done | dead | blocked</summary>
    public string Status { get; set; } = InvoiceDispatchStatuses.Pending;
    public Guid? IntegrationContractId { get; set; }
    public string? ProviderCode { get; set; }
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 6;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public Dictionary<string, object>? RequestSnapshot { get; set; }
    public Dictionary<string, object>? ResponseSnapshot { get; set; }

    public Invoice Invoice { get; set; } = null!;
}

public static class InvoiceDispatchActions
{
    public const string Send = "send";
    public const string Cancel = "cancel";
    public const string Status = "status";
}

public static class InvoiceDispatchStatuses
{
    public const string Pending = "pending";
    public const string Done = "done";
    public const string Dead = "dead";
    public const string Blocked = "blocked";
    public static readonly string[] All = [Pending, Done, Dead, Blocked];
}
