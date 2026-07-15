namespace ECSPros.Core.Domain.Entities;

public class PlatformSchemaField
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> LabelI18n { get; set; } = new();
    /// <summary>text | password | number | date | boolean</summary>
    public string Type { get; set; } = "text";
    /// <summary>credentials | settings</summary>
    public string Section { get; set; } = "credentials";
    public bool Required { get; set; } = false;
    /// <summary>Alan yanındaki info ikonunda gösterilen açıklama — null ise ikon çıkmaz.</summary>
    public Dictionary<string, string>? HelpI18n { get; set; }
}
