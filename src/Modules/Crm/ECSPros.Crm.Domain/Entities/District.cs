using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class District : BaseEntity
{
    public Guid CityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public City City { get; set; } = null!;
    public ICollection<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();
}
