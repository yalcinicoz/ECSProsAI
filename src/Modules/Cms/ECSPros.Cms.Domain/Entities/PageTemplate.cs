using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class PageTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public string DefaultLayout { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Page> Pages { get; set; } = new List<Page>();
}
