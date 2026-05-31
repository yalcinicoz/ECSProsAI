/**
 * Tüm i18n form alanları için dil bazlı etiketler.
 * I18nField bileşeninin `fields[].labels` prop'una doğrudan verilir.
 *
 * Kullanım:
 *   import { FL } from '@/lib/field-labels'
 *   fields={[{ key: 'name', labels: FL.name, required: true }]}
 */

type LabelMap = Record<string, string>

function lbl(tr: string, en: string, de = en, fr = en, ar = tr): LabelMap {
  return { tr, en, de, fr, ar }
}

export const FL: Record<string, LabelMap> = {
  // ── Genel ──────────────────────────────────────────────
  name:             lbl('Ad',          'Name',           'Name',          'Nom',           'الاسم'),
  title:            lbl('Başlık',      'Title',          'Titel',         'Titre',         'العنوان'),
  description:      lbl('Açıklama',    'Description',    'Beschreibung',  'Description',   'الوصف'),
  shortDescription: lbl('Kısa Açıklama','Short Description','Kurzbeschreibung','Courte description','وصف قصير'),
  content:          lbl('İçerik',      'Content',        'Inhalt',        'Contenu',       'المحتوى'),
  slug:             lbl('Slug',        'Slug',           'Slug',          'Slug',          'الرابط'),
  metaTitle:        lbl('Meta Başlık', 'Meta Title',     'Meta-Titel',    'Méta-titre',    'عنوان ميتا'),
  metaDescription:  lbl('Meta Açıklama','Meta Description','Meta-Beschreibung','Méta-description','وصف ميتا'),

  // ── Ürün / Katalog ──────────────────────────────────────
  productName:      lbl('Ürün Adı',    'Product Name',   'Produktname',   'Nom du produit','اسم المنتج'),
  displayName:      lbl('Görüntülenen Ad','Display Name','Anzeigename',   'Nom affiché',  'الاسم المعروض'),
  unit:             lbl('Birim',        'Unit',           'Einheit',       'Unité',        'الوحدة'),
  brand:            lbl('Marka',        'Brand',          'Marke',         'Marque',       'العلامة التجارية'),
  categoryName:     lbl('Kategori Adı','Category Name',  'Kategoriename', 'Nom de catégorie','اسم الفئة'),
  filterName:       lbl('Filtre Adı',  'Filter Name',    'Filtername',    'Nom du filtre',  'اسم الفلتر'),
  groupName:        lbl('Grup Adı',    'Group Name',     'Gruppenname',   'Nom du groupe', 'اسم المجموعة'),
  attributeName:    lbl('Özellik Adı', 'Attribute Name', 'Attributname',  'Nom d\'attribut','اسم السمة'),
  valueName:        lbl('Değer Adı',   'Value Name',     'Wertname',      'Nom de la valeur','اسم القيمة'),

  // ── Müşteri / CRM ───────────────────────────────────────
  firstName:        lbl('Ad',          'First Name',     'Vorname',       'Prénom',       'الاسم الأول'),
  lastName:         lbl('Soyad',       'Last Name',      'Nachname',      'Nom de famille','اسم العائلة'),
  address:          lbl('Adres',       'Address',        'Adresse',       'Adresse',      'العنوان'),
  city:             lbl('Şehir',       'City',           'Stadt',         'Ville',        'المدينة'),
  notes:            lbl('Notlar',      'Notes',          'Notizen',       'Notes',        'ملاحظات'),

  // ── Finans / Ticaret ────────────────────────────────────
  supplierName:     lbl('Tedarikçi Adı','Supplier Name', 'Lieferantenname','Nom du fournisseur','اسم المورد'),
  invoiceTitle:     lbl('Fatura Başlığı','Invoice Title','Rechnungstitel','Titre de facture','عنوان الفاتورة'),
  paymentTerms:     lbl('Ödeme Koşulları','Payment Terms','Zahlungsbedingungen','Conditions de paiement','شروط الدفع'),

  // ── CMS ─────────────────────────────────────────────────
  pageTitle:        lbl('Sayfa Başlığı','Page Title',    'Seitentitel',   'Titre de la page','عنوان الصفحة'),
  pageContent:      lbl('Sayfa İçeriği','Page Content',  'Seiteninhalt',  'Contenu de la page','محتوى الصفحة'),
  menuLabel:        lbl('Menü Etiketi', 'Menu Label',    'Menübezeichnung','Étiquette de menu','تسمية القائمة'),

  // ── Promosyon ───────────────────────────────────────────
  campaignName:     lbl('Kampanya Adı','Campaign Name',  'Kampagnenname', 'Nom de la campagne','اسم الحملة'),
  couponName:       lbl('Kupon Adı',   'Coupon Name',    'Kuponname',     'Nom du coupon', 'اسم القسيمة'),
}
