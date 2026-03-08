using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Neighborhood : BaseEntity
{
    public Guid DistrictId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public District District { get; set; } = null!;
    public ICollection<Street> Streets { get; set; } = new List<Street>();
}
