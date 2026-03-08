using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class SectionType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public Dictionary<string, object> SettingsSchema { get; set; } = new();
    public bool SupportsItems { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<PageSection> Sections { get; set; } = new List<PageSection>();
}
