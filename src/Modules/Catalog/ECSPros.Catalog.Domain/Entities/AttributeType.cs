using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class AttributeType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string DataType { get; set; } = "select"; // select, multi_select, text, number, boolean
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public bool RequiresFilterColor { get; set; } = false;

    public ICollection<AttributeValue> Values { get; set; } = new List<AttributeValue>();
    public ICollection<ProductGroupAttribute> ProductGroupAttributes { get; set; } = new List<ProductGroupAttribute>();
    // Bu tip bir varyant ekseni olarak kullanıldığında tanımlanan alt özellikler
    public ICollection<ProductGroupAxisSubAttribute> AxisSubAttributes { get; set; } = new List<ProductGroupAxisSubAttribute>();
    // Bu tip bir alt özellik olarak kullanıldığında sahip olduğu kayıtlar
    public ICollection<ProductGroupAxisSubAttribute> AsSubAttributeOf { get; set; } = new List<ProductGroupAxisSubAttribute>();
}
