using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class Content : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string ContentText { get; set; } = string.Empty;
}
