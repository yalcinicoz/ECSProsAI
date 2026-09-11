using ECSPros.Core.Domain.Entities;

namespace ECSPros.Api.Extensions;

/// <summary>Only the configuration form contract; does not enable reporting or send requests.</summary>
public static class OpenAiReportingCatalog
{
    public const string Code = "openai_reporting";
    public const string ServiceType = "ai_reporting";

    public static List<PlatformSchemaField> CreateSchema() => new()
    {
        new()
        {
            Key = "apiKey", Type = "password", Section = "credentials", Required = true,
            LabelI18n = new() { ["tr"] = "OpenAI API Anahtarı" },
            HelpI18n = new() { ["tr"] = "Firma hesabının API anahtarını girin; şifreli saklanır. ChatGPT giriş şifrenizi girmeyin." }
        },
        new()
        {
            Key = "model", Type = "text", Section = "settings", Required = true,
            LabelI18n = new() { ["tr"] = "Model Kimliği" },
            HelpI18n = new() { ["tr"] = "OpenAI hesabınızda erişilebilen modelin kimliği. Kaydetme işlemi model erişimini doğrulamaz; raporlama henüz etkin değildir." }
        }
    };
}
