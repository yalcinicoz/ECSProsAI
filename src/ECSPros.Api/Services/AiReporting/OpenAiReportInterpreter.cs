using System.Net.Http.Headers;
using System.Text;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Transport only; not exposed by a controller until firm resolution and privacy approval.</summary>
public sealed class OpenAiReportInterpreter(HttpClient client)
{
    public async Task<ReportInterpretation> InterpretAsync(string apiKey, string model, ApprovedReportPrompt prompt,
        IReadOnlySet<string> permissions, CancellationToken ct, IReadOnlyList<ReportField>? attributes = null,
        ReportConversation? conversation = null, string subject = "stock", int planVersion = 1)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsWhiteSpace))
            return new("configuration_error", "OpenAI API anahtarı eksik veya geçersiz.", null);
        // Build/authorize before any network request. No results/customer records are accepted here.
        ArgumentNullException.ThrowIfNull(prompt);
        if (planVersion is not (1 or 2) || planVersion == 2 && !DynamicReportPlan.SupportsSource(subject)) throw new ArgumentException("Plan sürümü desteklenmiyor.");
        var body = planVersion == 2 ? OpenAiDynamicReportContract.CreateRequest(model, prompt.Value, permissions, conversation, subject, attributes)
            : OpenAiReportContract.CreateRequest(model, prompt.Value, permissions, attributes, conversation, subject);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                return new("provider_error", "OpenAI isteği tamamlanamadı; bağlantı, model erişimi ve kotayı kontrol edin.", null);
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(chunk, deadline.Token)) > 0)
            {
                if (buffer.Length + count > OpenAiResponseLimits.EnvelopeBytes)
                    return new("error", "AI yanıtı izin verilen boyutu aştı.", null);
                buffer.Write(chunk, 0, count);
            }
            var responseText = Encoding.UTF8.GetString(buffer.ToArray());
            return planVersion == 2 ? OpenAiDynamicReportContract.ParseResponse(responseText, permissions, subject, attributes)
                : OpenAiReportContract.ParseResponse(responseText, permissions, attributes, subject);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new("timeout", "AI yanıtı zamanında alınamadı; rapor çalıştırılmadı.", null); }
        catch (HttpRequestException)
        { return new("provider_error", "OpenAI bağlantısı kurulamadı.", null); }
    }
}
