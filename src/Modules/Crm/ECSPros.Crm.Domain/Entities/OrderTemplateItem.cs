using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class OrderTemplateItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public string UnitType { get; set; } = "piece";
    public int SortOrder { get; set; } = 0;

    public OrderTemplate Template { get; set; } = null!;
}
