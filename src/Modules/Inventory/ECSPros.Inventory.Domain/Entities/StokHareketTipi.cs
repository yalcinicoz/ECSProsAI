namespace ECSPros.Inventory.Domain.Entities;

/// <summary>Stok hareketi tipi sözlüğü (FAZ 15.3 raf ekranları, 2026-09-10). Eski serbest metinler korunur;
/// yeni yazımlar buradan.</summary>
public static class StokHareketTipi
{
    public const string Purchase = "purchase";            // tedarik girişi (göze)
    public const string Sale = "sale";                    // sipariş kargoya verildi
    public const string Return = "return";                // müşteri iadesi depoya girdi (kapalı kısım)
    public const string Transfer = "transfer";            // göz→göz (aynı depo) / depo→depo
    public const string Adjustment = "adjustment";        // serbest giriş/çıkış, sayım farkı
    public const string ReturnToShelf = "return_to_shelf";// iade kısmından satış rafına
    public const string StoreMove = "store_move";         // mağaza reyonu ↔ merkez (anlık transfer)
    public const string PosSale = "pos_sale";
    public const string PosRefund = "pos_refund";
}
