using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Manken/model kadrosu — sadece admin panelde seçim/otomatik-tamamlama kaynağı.
/// Ürün tarafından FK ile referans alınmaz; ProductAttribute.CustomValue içindeki
/// mankenId düz bir değerdir (bkz. docs/manken-ozelligi-spec.md).
/// </summary>
public class Mannequin : BaseEntity
{
    public string? Code { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public int? HeightCm { get; set; }
    public int? WeightKg { get; set; }
    public int? ChestCm { get; set; }
    public int? WaistCm { get; set; }
    public int? HipCm { get; set; }
    public string? DefaultWornSize { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
