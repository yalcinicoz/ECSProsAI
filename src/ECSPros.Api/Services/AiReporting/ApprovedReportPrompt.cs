using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>
/// Explicitly acknowledged input that passed conservative local checks. NOT a guarantee of
/// anonymization or a replacement for the deployment's data-transfer/privacy policy.
/// </summary>
public sealed class ApprovedReportPrompt
{
    [JsonIgnore] public string Value { get; }
    private ApprovedReportPrompt(string value) => Value = value;
    public override string ToString() => "Rapor isteği (içerik gösterilmez)";

    private static readonly Regex[] SensitivePatterns =
    {
        Pattern(@"[^\s@]+@[^\s@]+\.[^\s@]+"),
        Pattern(@"(?<!\w)(?:\+?\d[\s().-]*){10,}(?!\w)"), // phone/identity/card-like numbers
        Pattern(@"\b[A-Z]{2}\s*\d{2}(?:\s*[A-Z0-9]){10,}\b"), // IBAN-like
        Pattern(@"\bsk-[A-Z0-9_-]{8,}"),
        Pattern(@"-----BEGIN [A-Z ]*PRIVATE KEY-----"),
        Pattern(@"\b(?:password|secret|token|api[ _-]?key|şifre|sifre)\s*[:=]"),
        Pattern(@"https?://|www\.")
    };

    public static ApprovedReportPrompt Create(string? value, bool transmissionConfirmed)
    {
        if (!transmissionConfirmed)
            throw new ArgumentException("İstek metninin OpenAI'a gönderileceğini onaylayın.");
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000)
            throw new ArgumentException("Rapor isteği 1–2000 karakter olmalı.");
        // Reject invisible formatting rather than silently removing/modifying the user's request.
        if (value.Any(c => char.GetUnicodeCategory(c) == UnicodeCategory.Format
            || (char.IsControl(c) && c is not ('\r' or '\n' or '\t'))))
            throw new ArgumentException("İstekte görünmez veya geçersiz karakterler var.");
        var normalized = value.Normalize(NormalizationForm.FormKC);
        if (SensitivePatterns.Any(pattern => pattern.IsMatch(normalized)))
            throw new ArgumentException("İstek kişisel bilgi, bağlantı veya gizli anahtar içeriyor olabilir. Bunları kaldırıp tekrar deneyin.");
        return new(value.Trim());
    }

    private static Regex Pattern(string pattern) => new(pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
