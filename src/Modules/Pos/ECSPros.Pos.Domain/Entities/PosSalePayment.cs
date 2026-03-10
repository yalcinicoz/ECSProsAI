using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosSalePayment : BaseEntity
{
    public Guid SaleId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty; // cash | credit_card | bank_transfer | gift_card
    public decimal Amount { get; set; }
    public decimal? TenderedAmount { get; set; }  // verilen para (nakit)
    public decimal? ChangeAmount { get; set; }    // üstü

    public PosSale Sale { get; set; } = null!;
}
