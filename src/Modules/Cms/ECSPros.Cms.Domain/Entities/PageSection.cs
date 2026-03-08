using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class PageSection : BaseEntity
{
    public Guid PageId { get; set; }
    public Guid SectionTypeId { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, string>? TitleI18n { get; set; }
    public Dictionary<string, string>? SubtitleI18n { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public Dictionary<string, object>? LayoutSettings { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public DateTime? VisibleFrom { get; set; }
    public DateTime? VisibleUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public Page Page { get; set; } = null!;
    public SectionType SectionType { get; set; } = null!;
    public ICollection<PageSectionItem> Items { get; set; } = new List<PageSectionItem>();
}
