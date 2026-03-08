using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class PageSectionItem : BaseEntity
{
    public Guid SectionId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Dictionary<string, string>? TitleI18n { get; set; }
    public Dictionary<string, string>? SubtitleI18n { get; set; }
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, string>? ImageAltI18n { get; set; }
    public string? MobileImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? LinkType { get; set; }
    public Guid? LinkTargetId { get; set; }
    public string? LinkUrl { get; set; }
    public Dictionary<string, string>? ButtonTextI18n { get; set; }
    public string? ButtonStyle { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public Dictionary<string, string>? CustomHtmlI18n { get; set; }
    public Dictionary<string, string>? BadgeTextI18n { get; set; }
    public string? BadgeColor { get; set; }
    public bool OpenInNewTab { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public PageSection Section { get; set; } = null!;
}
