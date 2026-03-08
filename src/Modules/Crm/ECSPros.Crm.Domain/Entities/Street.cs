using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Street : BaseEntity
{
    public Guid NeighborhoodId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public Neighborhood Neighborhood { get; set; } = null!;
    public ICollection<Building> Buildings { get; set; } = new List<Building>();
}
