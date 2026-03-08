using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class City : BaseEntity
{
    public Guid CountryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Country Country { get; set; } = null!;
    public ICollection<District> Districts { get; set; } = new List<District>();
}
