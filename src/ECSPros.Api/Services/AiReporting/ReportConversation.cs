using System.Text.Json.Serialization;

namespace ECSPros.Api.Services.AiReporting;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportConversationTurn(string? Prompt, string? Clarification);

/// <summary>Revalidated client history is context, never authority. No results or free assistant text.</summary>
public sealed class ReportConversation
{
    public const int MaxTurns = 8;
    public const int MaxCharacters = 6000;
    private readonly IReadOnlyList<(ApprovedReportPrompt Prompt, string Clarification)> turns;
    private ReportConversation(IReadOnlyList<(ApprovedReportPrompt, string)> turns) => this.turns = turns;
    public override string ToString() => "Rapor konuşması (içerik gösterilmez)";

    public static ReportConversation Create(ReportConversationTurn[]? history, ApprovedReportPrompt current, bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!confirmed) throw new ArgumentException("Konuşmanın OpenAI'a gönderileceğini onaylayın.");
        if ((history?.Length ?? 0) > MaxTurns)
            throw new ArgumentException("Konuşma sınırına ulaşıldı. Yeni rapor başlatın; önceki seçimler otomatik silinmedi.");
        var approved = new List<(ApprovedReportPrompt, string)>();
        var length = current.Value.Length;
        foreach (var turn in history ?? Array.Empty<ReportConversationTurn>())
        {
            if (turn is null || turn.Clarification is not ("none" or "stock_type" or "grouping" or "unsupported" or "order_dates" or "order_layout" or "dynamic_layout" or "stock_grain" or "stock_attribute"))
                throw new ArgumentException("Rapor konuşmasının biçimi geçerli değil.");
            var prompt = ApprovedReportPrompt.Create(turn.Prompt, confirmed);
            length += prompt.Value.Length;
            if (length > MaxCharacters) throw new ArgumentException("Konuşma çok uzun. Yeni rapor başlatın.");
            approved.Add((prompt, turn.Clarification));
        }
        return new(approved.AsReadOnly());
    }

    internal IReadOnlyList<ReportConversationMessage> Messages(string current)
    {
        var messages = new List<ReportConversationMessage>();
        foreach (var turn in turns)
        {
            messages.Add(new("user", turn.Prompt.Value));
            // Never accept client-supplied system/developer roles or assistant instructions.
            if (turn.Clarification != "none")
                messages.Add(new("assistant", Question(turn.Clarification)));
        }
        messages.Add(new("user", current));
        return messages;
    }

    public static string Question(string code) => code switch
    {
        "stock_type" => "Fiziksel stok, sanal stok veya ikisini birlikte mi istiyorsunuz?",
        "stock_grain" => "Stok eşiğini her barkodun tüm depolardaki toplamına mı, yoksa her konumun stok adedine ayrı ayrı mı uygulayalım?",
        "stock_attribute" => "Belirttiğiniz grup/değerin alanını netleştirir misiniz? Gerçek ürün grubu mu, yoksa bir özellik mi? Özellikse alan adı ve değerini yazın.",
        "grouping" => "Stokları hangi kırılımda görmek istersiniz? Ürün, depo, bir ürün özelliği veya genel toplam seçebilirsiniz.",
        "unsupported" => "İstenen alan veya filtre henüz desteklenmiyor; rapor kapsamını netleştirin.",
        "order_dates" => "Raporu hangi tarih aralığı için istiyorsunuz? Örneğin geçen ay veya başlangıç/bitiş tarihi yazabilirsiniz.",
        "order_layout" => "Siparişleri tek tek detay listesi olarak mı, yoksa durum/kanal bazında özet veya genel toplam olarak mı görmek istersiniz?",
        "dynamic_layout" => "Kayıtları tek tek detay olarak mı, yoksa özet olarak mı görmek istersiniz? Detayda kolonları; özette kırılım ve ölçüleri belirtebilirsiniz.",
        _ => throw new ArgumentException("Netleştirme kodu geçersiz.")
    };
}

internal sealed record ReportConversationMessage(string Role, string Content);
