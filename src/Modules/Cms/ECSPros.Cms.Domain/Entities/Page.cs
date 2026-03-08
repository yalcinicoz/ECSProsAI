using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class Page : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string> SlugI18n { get; set; } = new();
    public string PageType { get; set; } = string.Empty;
    public string? TargetGender { get; set; }
    public Guid? TargetCategoryId { get; set; }
    public Dictionary<string, string>? MetaTitleI18n { get; set; }
    public Dictionary<string, string>? MetaDescriptionI18n { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? PublishAt { get; set; }
    public DateTime? UnpublishAt { get; set; }

    public PageTemplate Template { get; set; } = null!;
    public ICollection<PageSection> Sections { get; set; } = new List<PageSection>();
}
