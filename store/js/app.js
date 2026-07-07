/* ═══════════════════════════════════════════════════════════
   ECSPros Store — app.js
   Vanilla JS · History API Router · No build step
═══════════════════════════════════════════════════════════ */

// ─────────────────────────────────────────────────────────
// CONFIG
// ─────────────────────────────────────────────────────────
const CFG = {
  API:    '/api',
  FPID:   'c900c659-8d0f-4754-9658-aa157ea3072e', // mishar platform
  LANG:   'tr',
  CUR:    'TRY',
};

// Navigation cache — loaded once at boot
const NAV = { cats: [], bySlug: {}, byId: {}, roots: [], children: {} };

// Session ID (guest cart)
if (!localStorage.getItem('ecspros_sid')) {
  localStorage.setItem('ecspros_sid', 'sess_' + crypto.getRandomValues(new Uint32Array(2)).join(''));
}
const SID = localStorage.getItem('ecspros_sid');

// ─────────────────────────────────────────────────────────
// HELPERS
// ─────────────────────────────────────────────────────────
const $ = id => document.getElementById(id);
const qs = (sel, ctx = document) => ctx.querySelector(sel);
const qsa = (sel, ctx = document) => [...ctx.querySelectorAll(sel)];

function t(i18n) {
  if (!i18n) return '';
  if (typeof i18n === 'string') return i18n;
  return i18n[CFG.LANG] || i18n.tr || i18n.en || Object.values(i18n)[0] || '';
}

function fmt(n) {
  if (n == null) return '—';
  return new Intl.NumberFormat('tr-TR', {
    style: 'currency', currency: 'TRY',
    minimumFractionDigits: 2, maximumFractionDigits: 2,
  }).format(n);
}

function imgSrc(url) {
  if (!url) return null;
  if (url.startsWith('http')) return url;
  return url.startsWith('/') ? url : '/' + url;
}

function escHtml(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function toast(msg, type = '') {
  const wrap = $('toastStack');
  if (!wrap) return;
  const cls = type === 'ok' ? 'ms-bildirim-basarili' : type === 'err' ? 'ms-bildirim-hata' : 'ms-bildirim-uyari';
  const baslik = type === 'ok' ? 'Başarılı:' : type === 'err' ? 'Hata:' : 'Bilgi:';
  const d = document.createElement('div');
  d.className = 'ms-bildirim ' + cls;
  d.innerHTML = `<strong class="ms-bildirim-baslik">${baslik}</strong> ${escHtml(msg)}`;
  wrap.appendChild(d);
  setTimeout(() => d.remove(), 3200);
}

function navigate(path, push = true) {
  const url = path.startsWith('/') ? path : '/' + path;
  if (push) history.pushState(null, '', url);
  router.route();
}

function setLoading(html = '') {
  const m = $('main');
  if (m) m.innerHTML = html || `<div style="padding:80px 40px;text-align:center;color:var(--ink-40);font-size:14px">Yükleniyor…</div>`;
}

// ─────────────────────────────────────────────────────────
// API CLIENT
// ─────────────────────────────────────────────────────────
const api = {
  async _req(path, method = 'GET', body = null) {
    const opts = { method, headers: { 'Content-Type': 'application/json' } };
    if (body) opts.body = JSON.stringify(body);
    const res = await fetch(CFG.API + path, opts);
    const json = await res.json();
    if (!json.success) throw new Error(json.error || 'İstek başarısız.');
    return json.data;
  },

  channelCategories(activeOnly = true) {
    return this._req(`/store/catalog/channel-categories?firmPlatformId=${CFG.FPID}&activeOnly=${activeOnly}`);
  },

  channelCategoryProducts(id, { page = 1, pageSize = 24 } = {}) {
    const p = new URLSearchParams({ page, pageSize });
    return this._req(`/store/catalog/channel-categories/${id}/products?${p}`);
  },

  channelCategoryFacets(id) {
    return this._req(`/store/catalog/channel-categories/${id}/facets`);
  },

  products({ page = 1, pageSize = 24, search = '' } = {}) {
    const p = new URLSearchParams({ firmPlatformId: CFG.FPID, page, pageSize });
    if (search) p.set('search', search);
    return this._req(`/store/catalog/products?${p}`);
  },

  productsFacets({ search = '' } = {}) {
    const p = new URLSearchParams({ firmPlatformId: CFG.FPID });
    if (search) p.set('search', search);
    return this._req(`/store/catalog/products/facets?${p}`);
  },

  product(code) {
    return this._req(`/store/catalog/products/${encodeURIComponent(code)}?firmPlatformId=${CFG.FPID}`);
  },

  getCart() {
    if (!Cart.id && !SID) return Promise.resolve({ items: [] });
    const p = new URLSearchParams({ sessionId: SID });
    if (Cart.id) p.set('cartId', Cart.id);
    p.set('firmPlatformId', CFG.FPID);
    return this._req(`/store/cart?${p}`);
  },

  addToCart(variantId, quantity, price) {
    return this._req('/store/cart/items', 'POST', {
      firmPlatformId: CFG.FPID,
      variantId, quantity, price,
      currencyCode: CFG.CUR,
      sessionId: SID,
    });
  },

  updateItem(cartId, itemId, quantity) {
    return this._req(`/store/cart/${cartId}/items/${itemId}`, 'PUT', { quantity });
  },

  removeItem(cartId, itemId) {
    return this._req(`/store/cart/${cartId}/items/${itemId}`, 'DELETE');
  },
};

// ─────────────────────────────────────────────────────────
// CART STATE
// ─────────────────────────────────────────────────────────
const Cart = {
  id:    localStorage.getItem('ecspros_cart') || null,
  items: [],

  saveId() { localStorage.setItem('ecspros_cart', this.id || ''); },

  get total()  { return this.items.reduce((s, i) => s + i.price * i.qty, 0); },
  get count()  { return this.items.reduce((s, i) => s + i.qty, 0); },

  async load() {
    try {
      const data = await api.getCart();
      if (data) {
        if (data.id) { this.id = data.id; this.saveId(); }
        this.items = (data.items || []).map(item => ({
          id:       item.id,
          variantId: item.variantId || item.productVariantId,
          name:     item.productName || item.variantName || item.name || item.variantSku || 'Ürün',
          sku:      item.variantSku  || item.sku || '',
          img:      item.imageUrl    || item.mainImageUrl || null,
          price:    item.unitPrice   || item.price || 0,
          qty:      item.quantity    || 1,
        }));
      }
    } catch {
      this.items = [];
    }
    this._updateBadge();
    renderCartPanel();
  },

  _updateBadge() {
    const badge = $('cartBadge');
    if (!badge) return;
    const n = this.count;
    badge.style.display = n > 0 ? 'flex' : 'none';
    badge.textContent   = n > 99 ? '99+' : n;
  },
};

// ─────────────────────────────────────────────────────────
// CART UI
// ─────────────────────────────────────────────────────────
function toggleCart() {
  window.msSepetMenuToggle?.();
}

function renderCartPanel() {
  const body    = $('cartPanelBody');
  const footer  = $('cartPanelFooter');
  const actions = $('cartPanelActions');
  const total   = $('cartTotal');
  const cnt     = $('cartItemCount');

  if (!body) return;

  if (!Cart.items.length) {
    body.innerHTML = `
      <div class="ms-ana-navigasyon-sepet-bos">
        <p>Sepetiniz boş</p>
        <a href="/urunler" class="ms-buton ms-buton-m ms-buton-sade">Alışverişe Başla</a>
      </div>`;
    if (footer) footer.style.display = 'none';
    if (actions) actions.style.display = 'none';
    if (cnt) cnt.textContent = '';
    return;
  }

  body.innerHTML = Cart.items.map(item => {
    const src = imgSrc(item.img);
    const img = src
      ? `<img class="ms-ana-navigasyon-sepet-urun-gorsel" src="${src}" alt="${escHtml(item.name)}" onerror="this.style.display='none'">`
      : `<div class="ms-ana-navigasyon-sepet-urun-gorsel"></div>`;
    return `
      <div class="ms-ana-navigasyon-sepet-urun">
        ${img}
        <div class="ms-ana-navigasyon-sepet-urun-bilgi">
          <strong class="ms-ana-navigasyon-sepet-urun-baslik">${escHtml(item.name)}</strong>
          ${item.sku ? `<p>${escHtml(item.sku)}</p>` : ''}
          <div class="ms-ana-navigasyon-sepet-urun-alt">
            <span>${item.qty} adet</span>
            <strong class="ms-fiyat-standart">${fmt(item.price * item.qty)}</strong>
          </div>
        </div>
        <button class="ms-ana-navigasyon-sepet-sil" type="button" onclick="cartRemove('${item.id}')" aria-label="${escHtml(item.name)} ürününü sepetten sil">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M18 6 6 18M6 6l12 12"/></svg>
        </button>
      </div>`;
  }).join('');

  if (cnt) cnt.textContent = `${Cart.count} ürün`;
  if (footer) footer.style.display = 'flex';
  if (actions) actions.style.display = 'grid';
  if (total) total.textContent = fmt(Cart.total);
}

window.cartQty = async (itemId, newQty) => {
  if (newQty < 1) { window.cartRemove(itemId); return; }
  try {
    if (Cart.id) await api.updateItem(Cart.id, itemId, newQty);
    const item = Cart.items.find(i => i.id === itemId);
    if (item) item.qty = newQty;
    Cart._updateBadge();
    renderCartPanel();
  } catch (e) { toast('Hata: ' + e.message, 'err'); }
};

window.cartRemove = async (itemId) => {
  try {
    if (Cart.id) await api.removeItem(Cart.id, itemId);
    Cart.items = Cart.items.filter(i => i.id !== itemId);
    Cart._updateBadge();
    renderCartPanel();
    toast('Ürün sepetten kaldırıldı.');
  } catch (e) { toast('Hata: ' + e.message, 'err'); }
};

async function addToCart(variantId, qty, price) {
  try {
    const res = await api.addToCart(variantId, qty, price);
    if (res?.cartId) { Cart.id = res.cartId; Cart.saveId(); }
    await Cart.load();
    toast('Ürün sepete eklendi!', 'ok');
    return true;
  } catch (e) {
    toast('Hata: ' + e.message, 'err');
    return false;
  }
}

// ─────────────────────────────────────────────────────────
// LISTING STATE — client-side filter + sort
// ─────────────────────────────────────────────────────────
const LS = {
  items:         [],
  facets:        null,   // StoreFacetsDto from API
  priceMin:      null,
  priceMax:      null,
  sort:          'default',
  selectedAttrs: new Set(),  // Set of valueId strings

  filtered() {
    let items = [...this.items];
    if (this.priceMin !== null) items = items.filter(p => (p.minPrice ?? p.basePrice ?? 0) >= this.priceMin);
    if (this.priceMax !== null) items = items.filter(p => (p.minPrice ?? p.basePrice ?? 0) <= this.priceMax);
    if (this.selectedAttrs.size > 0) {
      items = items.filter(p => {
        const colors = (p.colors || []).map(c => c.valueId);
        const attrs  = (p.attrs  || []).map(a => a.valueId);
        const allIds = colors.concat(attrs);
        return [...this.selectedAttrs].some(id => allIds.includes(id));
      });
    }
    if (this.sort === 'price_asc')  items.sort((a,b) => (a.minPrice ?? a.basePrice ?? 0) - (b.minPrice ?? b.basePrice ?? 0));
    if (this.sort === 'price_desc') items.sort((a,b) => (b.minPrice ?? b.basePrice ?? 0) - (a.minPrice ?? a.basePrice ?? 0));
    return items;
  },

  hasActiveFilters() {
    return this.priceMin !== null || this.priceMax !== null || this.selectedAttrs.size > 0;
  },

  _rerender() {
    const items = this.filtered();
    const grid  = $('prodGrid');
    if (grid) {
      grid.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 20)).join('')
        : `<p>Ürün bulunamadı. <a href="/urunler">Tüm ürünlere dön</a></p>`;
      window.msUrunKartDavranislariYenile?.(grid);
      window.msLazyLoadYenile?.(grid);
    }
    const cnt = $('resultCount');
    if (cnt) cnt.innerHTML = `<strong>${items.length.toLocaleString('tr-TR')}</strong> ürün listeleniyor`;
  },

  renderFacets() {
    if (!this.facets) return;

    // Price section
    const priceSection = $('facetPriceSection');
    if (priceSection && this.facets.priceMin != null) {
      $('facetPriceMin').placeholder = `Min ₺${Math.floor(this.facets.priceMin)}`;
      $('facetPriceMax').placeholder = `Max ₺${Math.ceil(this.facets.priceMax)}`;
    }

    // Attribute sections
    const container = $('facetAttrsContainer');
    if (!container || !this.facets.attributes?.length) return;

    container.innerHTML = this.facets.attributes.map(attr => {
      const isColor = attr.requiresFilterColor;
      const valuesHtml = attr.values.map(v => {
        const name = t(v.nameI18n);
        const id   = `fa_${v.valueId.replace(/-/g,'')}`;
        if (isColor) {
          return `
            <label class="ms-filtre-secim ms-filtre-renk-secim" data-filter-option>
              <input type="checkbox" id="${id}" value="${escHtml(v.valueId)}" onchange="_lsAttrToggle(this)" ${this.selectedAttrs.has(v.valueId) ? 'checked' : ''}>
              <span class="ms-filtre-renk" style="--ms-renk:${escHtml(v.hexCode || '#ccc')}" aria-hidden="true"></span>
              <span class="ms-filtre-renk-bilgi"><strong>${escHtml(name)}</strong> (${v.productCount})</span>
            </label>`;
        }
        return `
          <label class="ms-filtre-secim" data-filter-option>
            <input type="checkbox" id="${id}" value="${escHtml(v.valueId)}" onchange="_lsAttrToggle(this)" ${this.selectedAttrs.has(v.valueId) ? 'checked' : ''}>
            <span class="ms-filtre-kutu" aria-hidden="true"></span>
            ${escHtml(name)} <span>(${v.productCount})</span>
          </label>`;
      }).join('');

      const typeLabel = t(attr.typeNameI18n);
      return `
        <div class="ms-filtre-kapsayici" data-filter-block>
          <button class="ms-filtre-baslik" type="button" data-filter-toggle aria-expanded="true">
            ${escHtml(typeLabel)}
            <svg class="ms-filtre-ok ms-filtre-ok-acik" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
          </button>
          <div class="ms-filtre-icerik" data-filter-content>
            <div class="ms-filtre-secim-listesi${isColor ? ' ms-filtre-renk-listesi' : ''}">${valuesHtml}</div>
          </div>
        </div>`;
    }).join('');

    window.msFiltreBloklariBaslat?.(container);
  },
};

window._lsSort = val => { LS.sort = val; LS._rerender(); };

window._lsPriceFilter = () => {
  const mn = $('facetPriceMin');
  const mx = $('facetPriceMax');
  LS.priceMin = mn?.value ? parseFloat(mn.value) : null;
  LS.priceMax = mx?.value ? parseFloat(mx.value) : null;
  LS._rerender();
};

window._lsAttrToggle = (cb) => {
  const vid = cb.value;
  if (cb.checked) LS.selectedAttrs.add(vid);
  else            LS.selectedAttrs.delete(vid);
  LS._rerender();
};

window._lsClear = () => {
  LS.priceMin = null; LS.priceMax = null;
  LS.selectedAttrs.clear();
  const mn = $('facetPriceMin');
  const mx = $('facetPriceMax');
  if (mn) mn.value = ''; if (mx) mx.value = '';
  document.querySelectorAll('.ms-urun-listesi-filtre input[type=checkbox]').forEach(cb => cb.checked = false);
  LS._rerender();
};

// ─────────────────────────────────────────────────────────
// COMPONENTS
// ─────────────────────────────────────────────────────────
function prodCardHtml(p, delay = 0) {
  const src   = imgSrc(p.mainImageUrl);
  const name  = t(p.nameI18n);
  const brand = p.brandName || p.brand || '';
  const price = p.minPrice ?? p.basePrice ?? 0;
  const cmp   = p.compareAtPrice ?? null;
  const disc  = cmp && cmp > price ? Math.round((1 - price / cmp) * 100) : 0;

  const starsHtml = Array(5).fill(0).map((_, i) => `
    <svg width="11" height="11" viewBox="0 0 24 24" fill="${i < 4 ? 'currentColor' : 'none'}" stroke="currentColor" stroke-width="1.5">
      <polygon points="12,2 15.09,8.26 22,9.27 17,14.14 18.18,21.02 12,17.77 5.82,21.02 7,14.14 2,9.27 8.91,8.26"/>
    </svg>`).join('');

  // Renk rozeti: gerçek renk listesi varsa küçük daireler + sayaç, tıklayınca tooltip (site.js renkTooltipHazirla)
  const colors = p.colors || [];
  const selectedColorId = p.selectedColorValueId || null;
  const renkRozetHtml = colors.length > 0 ? `
    <span class="ms-urun-renk-rozet">
      <span class="ms-urun-renkler" aria-hidden="true">
        ${colors.slice(0, 3).map((c, i) => `<span class="ms-urun-renk" style="left:${i * 6}px;background:${escHtml(c.hexCode || '#ccc')}"></span>`).join('')}
      </span>
      ${colors.length}
    </span>
    <div class="ms-urun-renk-tooltip-alani">
      <span class="ms-urun-renk-tooltip">
        <span class="ms-urun-renk-tooltip-baslik">
          <span>Renk Seçenekleri <span class="ms-urun-renk-tooltip-sayac">(${colors.length})</span></span>
          <button class="ms-urun-renk-tooltip-kapat" type="button" data-ms-renk-tooltip-kapat aria-label="Renk seçeneklerini kapat">×</button>
        </span>
        <span class="ms-urun-renk-tooltip-liste">
          ${colors.map(c => `
            <a class="ms-urun-renk-tooltip-gorsel" href="/urun/${escHtml(p.code)}?color=${escHtml(c.valueId)}">
              <img data-ms-lazy-src="${escHtml(src || '')}" alt="${escHtml(t(c.nameI18n))} renk seçeneği">
              <span>${escHtml(t(c.nameI18n))}</span>
            </a>`).join('')}
        </span>
      </span>
    </div>` : '';

  const cardUrl = selectedColorId
    ? `/urun/${escHtml(p.code)}?color=${escHtml(selectedColorId)}`
    : `/urun/${escHtml(p.code)}`;

  const galeriResimler = [src].filter(Boolean).join('|');

  return `
    <div class="ms-urun-karti fade-up" style="animation-delay:${delay}ms" data-ms-kart-link-alani data-urun-kodu="${escHtml(p.code)}">
      <a class="ms-urun-kart-baglanti" href="${cardUrl}" data-ms-kart-link aria-label="${escHtml(name)} ürün detayına git"></a>
      <div class="ms-urun-gorsel-alani" data-ms-urun-galeri data-ms-urun-galeri-resimler="${escHtml(galeriResimler)}">
        <img class="ms-urun-gorsel" data-ms-urun-galeri-gorsel data-ms-lazy-src="${escHtml(src || '')}" alt="${escHtml(name)}" draggable="false">
        ${disc >= 5 ? `<span class="ms-urun-indirim-rozeti">-%${disc}</span>` : ''}
        <button class="ms-urun-favori" type="button" data-ms-urun-favori-kod="${escHtml(p.code)}" aria-label="Favorilere ekle" aria-pressed="false">
          <span class="ms-urun-favori-ikon"></span>
        </button>
        ${renkRozetHtml}
      </div>
      <div class="ms-urun-icerik">
        <h3 class="ms-urun-basligi">${brand ? `<strong>${escHtml(brand)}</strong> ` : ''}${escHtml(name)}</h3>
        <div class="ms-urun-puan">
          <span class="ms-urun-yildizlar" aria-label="4.3 yıldız">${starsHtml}</span>
          <span>4.3</span>
        </div>
        <div class="ms-urun-fiyat-senaryolari">
          ${disc >= 5
            ? `<p class="ms-urun-fiyat-satiri"><span class="ms-urun-indirim-rozeti">-%${disc}</span><span class="ms-urun-fiyat-indirimli">${fmt(price)}</span><span class="ms-urun-fiyat-eski">${fmt(cmp)}</span></p>`
            : `<p class="ms-urun-fiyat">${fmt(price)}</p>`}
        </div>
      </div>
    </div>`;
}

function skelGrid(n = 8) {
  return Array(n).fill(0).map(() => `
    <div class="animate-pulse">
      <div class="aspect-[3/4] w-full rounded-xl bg-slate-100"></div>
      <div class="mt-2 h-3 w-3/4 rounded bg-slate-100"></div>
      <div class="mt-1.5 h-3 w-2/5 rounded bg-slate-100"></div>
    </div>`).join('');
}

// Shared listing layout builder
function listingHtml({ title, crumbs = [], childCats = [], search = '' }) {
  const crumbsHtml = crumbs.map((c, i) =>
    i < crumbs.length - 1
      ? `<a href="${escHtml(c.href)}">${escHtml(c.label)}</a> <span>/</span>`
      : `<span>${escHtml(c.label)}</span>`
  ).join(' ');

  const subCatFilter = childCats.length ? `
    <div class="ms-filtre-kapsayici" data-filter-block>
      <button class="ms-filtre-baslik" type="button" data-filter-toggle aria-expanded="true">
        Alt Kategoriler
        <svg class="ms-filtre-ok ms-filtre-ok-acik" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
      </button>
      <div class="ms-filtre-icerik" data-filter-content>
        <div class="ms-filtre-secim-listesi">
          ${childCats.map(c => `
            <a class="ms-filtre-secim" href="/${escHtml(c.slug)}">${escHtml(t(c.nameI18n))}</a>`).join('')}
        </div>
      </div>
    </div>` : '';

  return `
    <div class="ms-urun-listesi-sayfa">
      <div class="ms-urun-listesi-grid">
        <!-- Sol filtre (masaüstü) -->
        <aside class="ms-urun-listesi-filtre">
          <button class="ms-buton ms-buton-s ms-buton-sade" type="button" id="filterClear" onclick="_lsClear()">Filtreleri Temizle</button>

          ${subCatFilter}

          <div class="ms-filtre-kapsayici" data-filter-block id="facetPriceSection">
            <button class="ms-filtre-baslik" type="button" data-filter-toggle aria-expanded="true">
              Fiyat Aralığı
              <svg class="ms-filtre-ok ms-filtre-ok-acik" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
            </button>
            <div class="ms-filtre-icerik" data-filter-content>
              <div class="ms-filtre-fiyat-araligi">
                <input type="number" id="facetPriceMin" placeholder="En Az" min="0">
                <span class="ms-filtre-fiyat-ayrac">–</span>
                <input type="number" id="facetPriceMax" placeholder="En Çok" min="0">
              </div>
              <button class="ms-filtre-uygula-buton" type="button" onclick="_lsPriceFilter()">Filtrele</button>
            </div>
          </div>

          <!-- Attribute facet'ler API cevabından sonra doldurulur -->
          <div id="facetAttrsContainer"></div>
        </aside>

        <!-- Sağ içerik -->
        <main class="ms-urun-listesi-icerik" aria-label="Ürün listeleme alanı">
          <section class="ms-urun-listesi-urun-alani lazy-infinite-on" data-ms-page-module="infinite-scroll" data-ms-infinite-scroll data-ms-infinite-yukleyici="urun-listesi" data-ms-infinite-esik="0.6" aria-label="Ürün listesi">
            <div class="ms-urun-listesi-ust-filtre-baslik">
              <div class="ms-urun-listesi-kategori-ozet">
                <h1>${escHtml(title)}</h1>
                <span id="resultCount">Yükleniyor…</span>
              </div>
              ${crumbsHtml ? `<nav class="ms-urun-detay-breadcrumb">${crumbsHtml}</nav>` : ''}
              <div class="ms-urun-listesi-filtre-satiri">
                <div></div>
                <div class="ms-urun-listesi-sag-araclar">
                  <div class="ms-siralama-select" data-ms-siralama-select>
                    <button class="ms-siralama-select-tetikleyici" type="button" data-ms-siralama-tetikleyici aria-haspopup="listbox" aria-expanded="false">
                      <span data-ms-siralama-deger>Önerilen</span>
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
                    </button>
                    <ul class="ms-siralama-select-panel" role="listbox">
                      <li data-ms-siralama-secenek="default" onclick="LS.sort='default';LS._rerender()">Önerilen</li>
                      <li data-ms-siralama-secenek="price_asc" onclick="LS.sort='price_asc';LS._rerender()">Fiyat: Düşükten Yükseğe</li>
                      <li data-ms-siralama-secenek="price_desc" onclick="LS.sort='price_desc';LS._rerender()">Fiyat: Yüksekten Düşüğe</li>
                    </ul>
                  </div>
                </div>
              </div>
            </div>

            <div class="ms-urun-listesi-urun-grid ms-kart-izgara" id="prodGrid" data-ms-infinite-liste aria-live="polite">${skelGrid(12)}</div>
            <div class="ms-infinite-ornek-yukleniyor" data-ms-infinite-yukleniyor>Daha fazla ürün yükleniyor…</div>
          </section>
        </main>
      </div>
    </div>`;
}

// ─────────────────────────────────────────────────────────
// PAGES
// ─────────────────────────────────────────────────────────

// ── HOME ─────────────────────────────────────────────────
async function pageHome() {
  $('main').innerHTML = `
    <!-- HERO -->
    <section class="bg-ms-siyah px-6 py-16 text-center text-white sm:py-20">
      <p class="text-[12px] font-semibold uppercase tracking-widest text-[var(--ms-renk-primary)]">ECSPros</p>
      <h1 class="mt-3 text-[28px] font-bold leading-tight sm:text-[38px]">Kaliteli Ürünler, Uygun Fiyatlar</h1>
      <p class="mx-auto mt-3 max-w-[520px] text-[13px] text-slate-300">Binlerce ürün, güvenli ödeme ve hızlı teslimat ile alışverişin keyfini keşfedin.</p>
      <a href="/urunler" class="ms-buton ms-buton-l ms-buton-birincil mt-6 inline-flex">Ürünleri Keşfet</a>
    </section>

    <!-- KATEGORİLER -->
    <section class="mx-auto max-w-[1400px] px-6 py-10">
      <div class="mb-5 flex items-center justify-between">
        <h2 class="text-[18px] font-bold text-ms-siyah">Kategorileri Keşfet</h2>
        <a class="text-[12px] font-semibold text-[var(--ms-renk-primary)]" href="/urunler">Tümünü Gör</a>
      </div>
      <div class="ms-gorunum-kategori-kapsul-listesi" id="homeCats">Yükleniyor…</div>
    </section>

    <!-- ÖNE ÇIKAN ÜRÜNLER -->
    <section class="mx-auto max-w-[1400px] px-6 py-10">
      <div class="mb-5 flex items-center justify-between">
        <h2 class="text-[18px] font-bold text-ms-siyah">Popüler Ürünler</h2>
        <a class="text-[12px] font-semibold text-[var(--ms-renk-primary)]" href="/urunler">Tüm Ürünler</a>
      </div>
      <div class="ms-kart-izgara lazy-infinite-on" id="homeFeat">${skelGrid(8)}</div>
    </section>
  `;

  const [catRes, prodRes] = await Promise.allSettled([
    NAV.roots.length ? Promise.resolve(NAV.roots) : api.channelCategories(),
    api.products({ page: 1, pageSize: 8 }),
  ]);

  const catEl = $('homeCats');
  if (catEl) {
    if (catRes.status === 'fulfilled') {
      const roots = Array.isArray(catRes.value) && catRes.value[0]?.slug
        ? catRes.value.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder)
        : NAV.roots;
      catEl.innerHTML = roots.length
        ? roots.slice(0, 8).map(c => `
            <a class="ms-gorunum-kategori-kapsul" href="/${escHtml(c.slug)}">
              <strong>${escHtml(t(c.nameI18n))}</strong>
            </a>`).join('')
        : '<p>Henüz kategori yok.</p>';
    } else {
      catEl.innerHTML = '<p>Kategoriler yüklenemedi.</p>';
    }
  }

  const featEl = $('homeFeat');
  if (featEl) {
    if (prodRes.status === 'fulfilled') {
      const items = prodRes.value.items || [];
      featEl.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 35)).join('')
        : '<p>Ürün bulunamadı.</p>';
      window.msUrunKartDavranislariYenile?.(featEl);
      window.msLazyLoadYenile?.(featEl);
    } else {
      featEl.innerHTML = '<p>Ürünler yüklenemedi.</p>';
    }
  }
}

// ── PRODUCTS ─────────────────────────────────────────────
// Ortak liste render'ı — sayfalama artık infinite-scroll ile ilerler (site.js'in
// window.msInfiniteLoaders motoru), hem /urunler hem kategori sayfası bunu paylaşır.
async function _renderListing({ title, crumbs, childCats = [], search = '', fetchPage, fetchFacets, emptyMsg }) {
  LS.items = []; LS.facets = null; LS.priceMin = null; LS.priceMax = null;
  LS.sort = 'default'; LS.selectedAttrs = new Set();
  LS.page = 1; LS.totalPages = 1;

  $('main').innerHTML = listingHtml({ title, crumbs, childCats, search });
  window.msRunPageModules(document);

  // Facet'ler grid'i BLOKLAMAZ: ürünler gelir gelmez render edilir, filtre
  // panelini facet cevabı geldiğinde ayrıca doldururuz (facets tüm katalogda
  // ağır bir aggregation — soğuk cache'te birkaç saniye sürebilir).
  const listingToken = Symbol();
  LS._listingToken = listingToken;
  fetchFacets().then(facets => {
    if (LS._listingToken !== listingToken) return; // kullanıcı başka sayfaya geçti
    LS.facets = facets;
    LS.renderFacets();
  }).catch(() => { /* filtre paneli boş kalır, sayfa çalışmaya devam eder */ });

  const prodRes = await Promise.allSettled([fetchPage(1)]).then(r => r[0]);

  const grid = $('prodGrid');
  try {
    if (prodRes.status === 'rejected') throw prodRes.reason;
    const data  = prodRes.value;
    const items = data.items || [];
    const total = data.totalCount ?? data.total ?? items.length;
    const ps    = data.pageSize || 24;
    LS.totalPages = Math.max(1, Math.ceil(total / ps));
    LS.items = items;

    const cnt = $('resultCount');
    if (cnt) cnt.innerHTML = `<strong>${total.toLocaleString('tr-TR')}</strong> ürün listeleniyor`;

    if (grid) {
      grid.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 25)).join('')
        : `<p>${escHtml(emptyMsg)}</p>`;
      window.msUrunKartDavranislariYenile?.(grid);
      window.msLazyLoadYenile?.(grid);
    }
  } catch (e) {
    if (grid) grid.innerHTML = `<p>Ürünler yüklenemedi: ${escHtml(e.message)}</p>`;
    return;
  }

  window.msInfiniteLoaders['urun-listesi'] = async () => {
    if (LS.page >= LS.totalPages) return false;
    LS.page += 1;
    try {
      const data = await fetchPage(LS.page);
      LS.items = LS.items.concat(data.items || []);
      LS._rerender();
    } catch { return false; }
    return LS.page < LS.totalPages;
  };
}

async function pageProducts({ search = '' } = {}) {
  const title = search ? `"${search}" Sonuçları` : 'Tüm Ürünler';
  const crumbs = [
    { label: 'Ana Sayfa', href: '/' },
    { label: 'Ürünler',   href: '/urunler' },
    ...(search ? [{ label: `"${search}"` }] : []),
  ];

  await _renderListing({
    title, crumbs, search,
    fetchPage:   (page) => api.products({ page, search, pageSize: 24 }),
    fetchFacets: () => api.productsFacets({ search }),
    emptyMsg: 'Ürün bulunamadı.',
  });
}

// ── CATEGORY ─────────────────────────────────────────────
async function pageCategory(slug, params = {}) {
  const cat = NAV.bySlug[slug];
  if (!cat) {
    setLoading();
    try {
      const allCats = normalizeList(await api.channelCategories());
      allCats.forEach(c => { NAV.bySlug[c.slug] = c; NAV.byId[c.id] = c; });
      NAV.cats = allCats;
      NAV.roots = allCats.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder);
    } catch { $('main').innerHTML = '<p style="padding:80px 40px">Kategori yüklenemedi.</p>'; return; }
    return pageCategory(slug, params);
  }

  const catName   = t(cat.nameI18n);
  const childCats = NAV.cats.filter(c => c.parentId === cat.id).sort((a, b) => a.sortOrder - b.sortOrder);
  const parentCat = cat.parentId ? NAV.byId[cat.parentId] : null;

  const crumbs = [
    { label: 'Ana Sayfa', href: '/' },
    ...(parentCat ? [{ label: t(parentCat.nameI18n), href: '/' + parentCat.slug }] : []),
    { label: catName },
  ];

  await _renderListing({
    title: catName, crumbs, childCats,
    fetchPage:   (page) => api.channelCategoryProducts(cat.id, { page, pageSize: 24 }),
    fetchFacets: () => api.channelCategoryFacets(cat.id),
    emptyMsg: 'Bu kategoride henüz ürün bulunmuyor.',
  });
}

// ── PRODUCT DETAIL ────────────────────────────────────────
async function pageProduct(code) {
  const colorParam = new URLSearchParams(window.location.search).get('color');

  $('main').innerHTML = `
    <div class="dp-wrap">
      <div class="dp-gallery">
        <div class="dp-thumbs"></div>
        <div class="dp-main"><div class="dp-main-ph">…</div></div>
      </div>
      <div class="dp-info">
        <div class="skel skel-line" style="width:40%;height:11px;margin:0 0 16px"></div>
        <div class="skel skel-line" style="width:85%;height:28px;margin:0 0 10px"></div>
        <div class="skel skel-line" style="width:40%;height:28px;margin:0 0 24px"></div>
        <div class="skel skel-line" style="width:100%;height:80px"></div>
      </div>
    </div>`;

  try {
    const product  = await api.product(code);
    const variants = (product.variants || []).filter(v => v.isActive);

    // Attribute map: code → { code, nameI18n, isColor, values }
    const attrMap = {};
    for (const v of variants) {
      for (const a of (v.attributes || [])) {
        if (!attrMap[a.attributeTypeCode]) {
          attrMap[a.attributeTypeCode] = {
            code: a.attributeTypeCode, nameI18n: a.attributeTypeNameI18n,
            isColor: a.isColor || false, values: [],
          };
        }
        if (!attrMap[a.attributeTypeCode].values.some(x => x.id === a.attributeValueId))
          attrMap[a.attributeTypeCode].values.push({
            id: a.attributeValueId, nameI18n: a.attributeValueNameI18n, hexCode: a.hexCode || null,
          });
      }
    }
    const attrTypes     = Object.values(attrMap);
    const colorAttrType = attrTypes.find(at => at.isColor) || null;

    // colorImgMap: colorValueId → first imageUrl (backend garanti ediyor: aynı renkli tüm varyantlar aynı görsele sahip)
    const colorImgMap = {};
    if (colorAttrType) {
      for (const val of colorAttrType.values) {
        // Rengin herhangi bir varyantında görsel varsa kaydet
        const cv = variants.find(v =>
          (v.attributes || []).some(a => a.attributeTypeCode === colorAttrType.code && a.attributeValueId === val.id)
          && (v.images || []).length > 0
        );
        if (cv?.images?.[0]) colorImgMap[val.id] = imgSrc(cv.images[0].imageUrl);
      }
    }

    const state = {
      product, variants, attrMap, attrTypes, colorAttrType,
      selected: {}, variant: variants.length === 1 ? variants[0] : null,
      qty: 1, imgIdx: 0,
    };

    if (colorParam && colorAttrType) {
      const found = colorAttrType.values.find(v => v.id === colorParam);
      if (found) state.selected[colorAttrType.code] = colorParam;
    }
    window.__dp = state;

    // Seçili renge göre görseller — aynı renkli ilk varyantın görselleri yeterli
    // (backend, aynı renge ait tüm varyantlara aynı görselleri atar)
    function colorImages() {
      const colorCode = state.colorAttrType?.code;
      const selColor  = colorCode ? state.selected[colorCode] : null;
      if (selColor) {
        const cv = variants.find(v =>
          (v.attributes || []).some(a => a.attributeTypeCode === colorCode && a.attributeValueId === selColor)
        );
        if (cv?.images?.length) return [...cv.images].sort((a,b) => a.sortOrder - b.sortOrder);
      }
      if (state.variant?.images?.length) return [...state.variant.images].sort((a,b) => a.sortOrder - b.sortOrder);
      // Fallback: ilk görseli olan varyantı kullan (flatMap çoğaltma yapar)
      const first = variants.find(v => v.images?.length > 0);
      return first ? [...first.images].sort((a,b) => a.sortOrder - b.sortOrder) : [];
    }

    // Seçili renge göre mevcut beden değerlerini döndürür
    function availSizeValues(at) {
      const colorCode = colorAttrType?.code;
      const selColor  = colorCode ? state.selected[colorCode] : null;
      if (!selColor) return at.values;
      return at.values.filter(val =>
        variants.some(v =>
          (v.attributes || []).some(a => a.attributeTypeCode === colorCode && a.attributeValueId === selColor)
          && (v.attributes || []).some(a => a.attributeTypeCode === at.code && a.attributeValueId === val.id)
        )
      );
    }

    // Seçili renk + bu beden kombinasyonunun toplam stoğu
    function sizeStock(at, valId) {
      const colorCode = colorAttrType?.code;
      const selColor  = colorCode ? state.selected[colorCode] : null;
      return variants
        .filter(v =>
          (v.attributes || []).some(a => a.attributeTypeCode === at.code && a.attributeValueId === valId)
          && (!selColor || (v.attributes || []).some(a => a.attributeTypeCode === colorCode && a.attributeValueId === selColor))
        )
        .reduce((sum, v) => sum + (v.stockQty ?? 0), 0);
    }

    function sizeOptsHtml(at) {
      return availSizeValues(at).map(val => {
        const oos = sizeStock(at, val.id) <= 0;
        const sel = state.selected[at.code] === val.id;
        return `<label class="ms-beden-secim${oos ? ' ms-beden-secim-tukendi' : ''}">
          <input class="ms-beden-secim-input" type="radio" name="dp-beden-${escHtml(at.code)}"
                 ${sel ? 'checked' : ''} ${oos ? 'disabled' : ''}
                 onchange="dpSelect('${at.code}','${val.id}',this)">
          <span class="ms-beden-secim-kutu">${escHtml(t(val.nameI18n))}</span>
        </label>`;
      }).join('');
    }

    function currentPrice() {
      if (state.variant) return state.variant.platformPrice ?? state.variant.basePrice;
      const colorCode = state.colorAttrType?.code;
      const selColor  = colorCode ? state.selected[colorCode] : null;
      const pool = selColor
        ? variants.filter(v => (v.attributes || []).some(a => a.attributeTypeCode === colorCode && a.attributeValueId === selColor))
        : variants;
      const prices = pool.map(v => v.platformPrice ?? v.basePrice).filter(Boolean);
      return prices.length ? Math.min(...prices) : null;
    }

    // Galeri markup'ı üretir (data-ms-urun-detay-resim-* — bkz. site.js msUrunDetayResimBaslat)
    function galleryHtml(imgs) {
      const name = escHtml(t(product.nameI18n));
      const thumbsHtml = imgs.map((img, i) => `
        <button class="ms-urun-detay-resim-thumb${i === 0 ? ' ms-urun-detay-resim-thumb-aktif' : ''}" type="button" data-ms-urun-detay-resim-thumb aria-label="${i + 1}. görseli göster">
          <img src="${imgSrc(img.imageUrl)}" alt="" loading="lazy">
        </button>`).join('');
      const slidesHtml = imgs.map((img, i) => `
        <div class="ms-urun-detay-resim-ana${i === 0 ? ' ms-urun-detay-resim-ana-gorunur ms-urun-detay-resim-ana-aktif' : ''}" data-ms-urun-detay-resim-slide>
          <img src="${imgSrc(img.imageUrl)}" alt="${name}" draggable="false">
        </div>`).join('');

      return `
        <div class="ms-urun-detay-resim-alani" data-ms-urun-detay-resim-alani>
          <div class="ms-urun-detay-resim-galeri">
            <div class="ms-urun-detay-resim-thumb-listesi">${thumbsHtml}</div>
            <div class="ms-urun-detay-resim-ana-sutun">
              <div class="ms-urun-detay-resim-ana-kapsayici" data-ms-urun-detay-resim-surukle>
                <div class="ms-urun-detay-resim-track" data-ms-urun-detay-resim-track>${slidesHtml}</div>
                ${imgs.length > 1 ? `
                  <button class="ms-urun-detay-resim-kontrol ms-urun-detay-resim-kontrol-sol" type="button" data-ms-urun-detay-resim-yon="onceki" aria-label="Önceki görsel">‹</button>
                  <button class="ms-urun-detay-resim-kontrol ms-urun-detay-resim-kontrol-sag" type="button" data-ms-urun-detay-resim-yon="sonraki" aria-label="Sonraki görsel">›</button>` : ''}
              </div>
            </div>
          </div>
          <div class="ms-ornek-modal ms-urun-detay-resim-modal" data-ms-urun-detay-resim-modal aria-hidden="true">
            <div class="ms-ornek-modal-kaplama" data-ms-urun-detay-resim-modal-kapat></div>
            <div class="ms-urun-detay-resim-modal-kutu" role="dialog" aria-modal="true" aria-label="Ürün görseli büyütülmüş">
              <button class="ms-ornek-modal-kapat" type="button" data-ms-urun-detay-resim-modal-kapat aria-label="Kapat">×</button>
              <img class="ms-urun-detay-resim-modal-gorsel" data-ms-urun-detay-resim-modal-gorsel alt="${name}">
            </div>
          </div>
        </div>`;
    }

    function renderGallery() {
      const imgs = colorImages();
      state.imgIdx = 0;
      const alan = qs('[data-ms-urun-detay-resim-alani]');
      if (!alan) return;
      alan.outerHTML = galleryHtml(imgs);
      window.msUrunDetayResimBaslat?.(document);
    }

    function render() {
      const imgs  = colorImages();
      const price = currentPrice();
      const cmp   = state.variant?.compareAtPrice ?? null;
      const disc  = cmp && price && cmp > price ? Math.round((1 - price / cmp) * 100) : 0;
      const canAdd = state.variant !== null;
      const desc  = t(product.shortDescriptionI18n);

      // Renk seçenekleri — sadece görseli olan renkler listelenir
      const visibleColors = colorAttrType
        ? colorAttrType.values.filter(val => colorImgMap[val.id])
        : [];
      const colorHtml = visibleColors.length > 0 ? `
        <section class="ms-urun-detay-renk-alani" aria-label="Renk seçenekleri">
          <p class="ms-urun-detay-renk-baslik">
            Renk: <strong id="vval-${colorAttrType.code}">${
              state.selected[colorAttrType.code]
                ? escHtml(t(colorAttrType.values.find(v => v.id === state.selected[colorAttrType.code])?.nameI18n) || '')
                : ''
            }</strong>
          </p>
          <div class="ms-urun-detay-renk-listesi">
            ${visibleColors.map(val => `
              <button class="ms-urun-detay-renk${state.selected[colorAttrType.code] === val.id ? ' ms-urun-detay-renk-aktif' : ''}"
                      type="button" aria-pressed="${state.selected[colorAttrType.code] === val.id}"
                      title="${escHtml(t(val.nameI18n))}"
                      onclick="dpSelect('${colorAttrType.code}','${val.id}',this)">
                <img src="${colorImgMap[val.id]}" alt="${escHtml(t(val.nameI18n))}" loading="lazy">
                <span class="ms-urun-detay-renk-secili-ikon" aria-hidden="true">✓</span>
              </button>`).join('')}
          </div>
        </section>` : '';

      // Diğer öznitelikler (beden vb.) — seçili renge göre filtrelenir
      const otherAttrsHtml = attrTypes
        .filter(at => !at.isColor)
        .map(at => `
          <section aria-label="${escHtml(t(at.nameI18n))} seçenekleri">
            <p class="ms-urun-detay-renk-baslik">${escHtml(t(at.nameI18n))}: <strong id="vval-${at.code}"></strong></p>
            <div class="ms-beden-secim-listesi" id="dp-size-${at.code}">${sizeOptsHtml(at)}</div>
          </section>`).join('');

      $('main').innerHTML = `
        <div class="ms-urun-detay-sayfa fade-up">
        <div class="ms-urun-detay-kapsayici">
          <div class="ms-urun-detay-ust">
            ${galleryHtml(imgs)}

            <div class="ms-urun-detay-bilgi">
              <h1 class="ms-urun-basligi">${escHtml(t(product.nameI18n))}</h1>
              <p class="ms-urun-detay-renk-baslik">${escHtml(product.code)}</p>

              <div class="ms-urun-fiyat-senaryolari">
                ${disc >= 5
                  ? `<p class="ms-urun-fiyat-satiri"><span class="ms-urun-indirim-rozeti">-%${disc}</span><span class="ms-urun-fiyat-indirimli">${price != null ? fmt(price) : '—'}</span><span class="ms-urun-fiyat-eski">${fmt(cmp)}</span></p>`
                  : `<p class="ms-urun-fiyat">${price != null ? fmt(price) : '—'}</p>`}
              </div>

              ${colorHtml}
              ${otherAttrsHtml}

              <div class="ms-urun-detay-cta">
                <button class="ms-buton ms-buton-l ms-buton-birincil ms-buton-tam" id="dpAtcBtn"
                        ${canAdd ? '' : 'disabled'}
                        onclick="dpAddToCart()">
                  ${canAdd ? 'Sepete Ekle' : 'Seçim Yapın'}
                </button>
                <button class="ms-urun-favori" type="button" data-ms-urun-favori-kod="${escHtml(product.code)}" aria-label="Favorilere ekle" aria-pressed="false">
                  <span class="ms-urun-favori-ikon"></span>
                </button>
              </div>

              ${desc ? `
              <div class="ms-footer-kolon" data-ms-footer-akordiyon>
                <button class="ms-footer-akordiyon-baslik" type="button" data-ms-footer-akordiyon-tetikleyici aria-expanded="false">
                  <span class="ms-footer-baslik">Ürün Açıklaması</span>
                  <span class="ms-footer-akordiyon-ok" aria-hidden="true"></span>
                </button>
                <div class="ms-footer-akordiyon-icerik" data-ms-footer-akordiyon-icerik>${escHtml(desc)}</div>
              </div>` : ''}
            </div>
          </div>
        </div>
        </div>`;

      window.msRunPageModules(document);
      window.msUrunKartDavranislariYenile?.($('main'));
    }

    // Attribute select
    window.dpSelect = (typeCode, valueId, btn) => {
      const s = window.__dp;
      s.selected[typeCode] = valueId;
      s.imgIdx = 0;

      const labelEl = $(`vval-${typeCode}`);
      if (labelEl) {
        const at  = s.attrMap[typeCode];
        const val = at.values.find(v => v.id === valueId);
        labelEl.textContent = val ? t(val.nameI18n) : '';
      }

      const opts = btn.closest('.ms-urun-detay-renk-listesi, .ms-beden-secim-listesi');
      if (opts) opts.querySelectorAll('.ms-urun-detay-renk').forEach(b => { b.classList.remove('ms-urun-detay-renk-aktif'); b.setAttribute('aria-pressed', 'false'); });
      if (btn.classList.contains('ms-urun-detay-renk')) { btn.classList.add('ms-urun-detay-renk-aktif'); btn.setAttribute('aria-pressed', 'true'); }

      if (s.colorAttrType && typeCode === s.colorAttrType.code) {
        // Renk değişince: seçili beden bu renkte yoksa temizle
        for (const at of s.attrTypes.filter(a => !a.isColor)) {
          const curSize = s.selected[at.code];
          const stillOk = !curSize || s.variants.some(v =>
            (v.attributes || []).some(a => a.attributeTypeCode === typeCode && a.attributeValueId === valueId)
            && (v.attributes || []).some(a => a.attributeTypeCode === at.code && a.attributeValueId === curSize)
          );
          if (!stillOk) {
            delete s.selected[at.code];
            const lbl = document.getElementById(`vval-${at.code}`);
            if (lbl) lbl.textContent = '';
          }
          const optsEl = document.getElementById(`dp-size-${at.code}`);
          if (optsEl) optsEl.innerHTML = sizeOptsHtml(at);
        }
        const url = new URL(window.location.href);
        url.searchParams.set('color', valueId);
        history.replaceState(null, '', url.toString());
        renderGallery();
      }

      s.variant = s.variants.find(v =>
        Object.entries(s.selected).every(([code, id]) =>
          (v.attributes || []).some(a => a.attributeTypeCode === code && a.attributeValueId === id)
        )
      ) ?? null;

      const priceEl = qs('.ms-urun-fiyat, .ms-urun-fiyat-indirimli');
      if (priceEl) priceEl.textContent = currentPrice() != null ? fmt(currentPrice()) : '—';

      const atcBtn = $('dpAtcBtn');
      if (atcBtn) {
        const canAdd = s.variant !== null;
        atcBtn.disabled = !canAdd;
        atcBtn.textContent = canAdd ? 'Sepete Ekle' : 'Seçim Yapın';
      }
    };

    // Add to cart
    window.dpAddToCart = async () => {
      const s   = window.__dp;
      const v   = s.variant;
      if (!v) { toast('Lütfen beden seçin.', 'err'); return; }
      const btn = $('dpAtcBtn');
      if (btn) { btn.disabled = true; btn.textContent = 'Ekleniyor…'; }
      const price = v.platformPrice ?? v.basePrice;
      await addToCart(v.id, s.qty, price);
      if (btn) { btn.disabled = false; btn.textContent = 'Sepete Ekle'; }
    };

    render();

  } catch (e) {
    $('main').innerHTML = `
      <div style="text-align:center;padding:100px 40px;color:var(--ink-40)">
        <div style="font-family:var(--font-disp);font-size:64px;margin-bottom:16px;opacity:.3">?</div>
        <h2 style="font-family:var(--font-disp);font-size:28px;margin-bottom:10px;color:var(--ink)">Ürün Bulunamadı</h2>
        <p style="font-size:14px;margin-bottom:28px">${escHtml(e.message)}</p>
        <a href="/urunler" style="color:var(--gold);font-size:12px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;border-bottom:1px solid currentColor;padding-bottom:2px">← Ürünlere Dön</a>
      </div>`;
  }
}

// ─────────────────────────────────────────────────────────
// NAV CATEGORIES — mega menu
// ─────────────────────────────────────────────────────────
async function initNav() {
  try {
    const allCats = normalizeList(await api.channelCategories());

    // Populate NAV cache
    NAV.cats = allCats;
    allCats.forEach(c => { NAV.bySlug[c.slug] = c; NAV.byId[c.id] = c; });
    NAV.roots = allCats.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder);
    const childrenOf = (id) => allCats.filter(c => c.parentId === id).sort((a, b) => a.sortOrder - b.sortOrder);

    // ── Masaüstü mega menü ──
    const megaIc = $('megaMenuIc');
    if (megaIc) {
      const solKolonHtml = NAV.roots.map(c => `
        <div class="ms-magaza-mega-kategori-grubu" data-ms-magaza-kategori-grubu="${escHtml(c.slug)}">
          <a class="ms-magaza-mega-sol-link" href="/${escHtml(c.slug)}" data-ms-magaza-kategori="${escHtml(c.slug)}">${escHtml(t(c.nameI18n))}</a>
        </div>`).join('');

      const panellerHtml = NAV.roots.map(c => {
        const subs = childrenOf(c.id);
        const linksHtml = subs.map(s => `
          <a class="ms-magaza-mega-resimli-link" href="/${escHtml(s.slug)}"><span>${escHtml(t(s.nameI18n))}</span></a>`).join('');
        return `
          <div class="ms-magaza-mega-icerik" data-ms-magaza-panel="${escHtml(c.slug)}">
            <section class="ms-magaza-mega-bolum">
              <span class="ms-magaza-mega-baslik">${escHtml(t(c.nameI18n))} Kategorileri</span>
              <div class="ms-magaza-mega-resimli-grid">
                ${linksHtml || '<p>Alt kategori yok.</p>'}
              </div>
            </section>
          </div>`;
      }).join('');

      const ustLinklerHtml = NAV.roots.map(c => `
        <a class="ms-magaza-menu-link" href="/${escHtml(c.slug)}" data-ms-magaza-menu-link="${escHtml(c.slug)}">${escHtml(t(c.nameI18n))}</a>`).join('');

      megaIc.innerHTML = `
        <div class="ms-magaza-menu-ogesi ms-magaza-menu-tum">
          <a class="ms-magaza-menu-link" href="/urunler">
            <svg class="ms-magaza-menu-link-ikon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="M3.75 5.25h16.5m-16.5 6h16.5m-16.5 6h16.5"/></svg>
            Kategoriler
          </a>
          <div class="ms-magaza-mega-menu" data-ms-magaza-mega-menu>
            <div class="ms-magaza-mega-sol-kolon">${solKolonHtml}</div>
            ${panellerHtml}
          </div>
        </div>
        <div class="ms-magaza-menu-kaydirma-grubu">
          <div class="ms-magaza-menu-kaydirma">${ustLinklerHtml}</div>
        </div>`;

      window.msMagazaMenuBaslat?.(document.querySelector('[data-ms-magaza-menu]'));
    }

    // ── Mobil off-canvas menü ──
    const mobilAnaSekmeler = $('mobilAnaSekmeler');
    const mobilMenuIcerik  = $('mobilMenuIcerik');
    if (mobilAnaSekmeler && mobilMenuIcerik) {
      mobilAnaSekmeler.innerHTML = NAV.roots.map((c, i) => `
        <button class="ms-ana-navigasyon-mobil-ana-sekme${i === 0 ? ' ms-ana-navigasyon-mobil-ana-sekme-aktif' : ''}" type="button" data-ms-mobil-ana-sekme="${escHtml(c.slug)}" aria-pressed="${i === 0 ? 'true' : 'false'}">${escHtml(t(c.nameI18n))}</button>`).join('');

      mobilMenuIcerik.innerHTML = NAV.roots.map((c, i) => {
        const subs = childrenOf(c.id);
        const gridHtml = subs.map(s => `
          <a class="ms-ana-navigasyon-mobil-kategori" href="/${escHtml(s.slug)}"><span>${escHtml(t(s.nameI18n))}</span></a>`).join('');
        return `
          <div class="ms-ana-navigasyon-mobil-yan-grup" data-ms-mobil-yan-grup="${escHtml(c.slug)}" ${i === 0 ? '' : 'hidden'}>
            <div class="ms-ana-navigasyon-mobil-grid">${gridHtml || '<p>Alt kategori yok.</p>'}</div>
          </div>`;
      }).join('');
    }

    // ── Ana sayfa mobil kategori şeridi ──
    const mobilSerit = $('mobilKategoriSeridi');
    const mobilSeritIcerik = $('mobilKategoriSeridiIcerik');
    if (mobilSerit && mobilSeritIcerik && window.location.pathname === '/') {
      mobilSeritIcerik.innerHTML = NAV.roots.map(c => `
        <a class="ms-magaza-menu-link" href="/${escHtml(c.slug)}">${escHtml(t(c.nameI18n))}</a>`).join('');
      mobilSerit.hidden = false;
    }

    // ── Footer kategorileri ──
    const footerCats = $('footerCats');
    if (footerCats) {
      footerCats.innerHTML = NAV.roots.map(c => `
        <a href="/${escHtml(c.slug)}">${escHtml(t(c.nameI18n))}</a>`).join('');
    }
  } catch(e) {
    console.error('Nav init failed:', e);
  }
}

// Aktif üst kategori linkini işaretle (mega menü hover state'iyle çakışmaz, sayfa yüklendiğinde çalışır)
function syncNavCats() {
  const path = window.location.pathname;
  qsa('[data-ms-magaza-menu-link]').forEach(a => {
    const href = (a.getAttribute('href') || '').split('?')[0];
    a.classList.toggle('ms-magaza-menu-link-aktif', href === path);
  });
}

// ── Nav arama: canlı sonuçlar ──
let _navSearchTimer = null;
window.msAramaSonuclariniGetir = (query) => {
  clearTimeout(_navSearchTimer);
  const el = $('navSearchResults');
  if (!el) return;

  if (!query) {
    el.innerHTML = `<p class="ms-ana-navigasyon-arama-kategori-label"><span>Aramaya başlamak için yazın.</span></p>`;
    return;
  }

  el.innerHTML = `<p class="ms-ana-navigasyon-arama-kategori-label"><span>Aranıyor…</span></p>`;
  _navSearchTimer = setTimeout(async () => {
    try {
      const data = await api.products({ search: query, page: 1, pageSize: 6 });
      const items = data.items || [];
      el.innerHTML = items.length
        ? `<div class="ms-ana-navigasyon-arama-kategori-label"><span>Arama Sonuçları</span><small>${items.length} ürün</small></div>
           <div class="ms-ana-navigasyon-arama-sonuc-listesi">${items.map(searchResultCardHtml).join('')}</div>
           <a class="ms-ana-navigasyon-tumunu-gor" href="/urunler?search=${encodeURIComponent(query)}">Tümünü Gör</a>`
        : `<p class="ms-ana-navigasyon-arama-kategori-label"><span>"${escHtml(query)}" için sonuç bulunamadı.</span></p>`;
    } catch {
      el.innerHTML = `<p class="ms-ana-navigasyon-arama-kategori-label"><span>Arama yapılamadı.</span></p>`;
    }
  }, 260);
};

function searchResultCardHtml(p) {
  const src = imgSrc(p.mainImageUrl);
  const price = p.minPrice ?? p.basePrice ?? 0;
  return `
    <a class="ms-search-urun-karti" href="/urun/${escHtml(p.code)}">
      <span class="ms-search-urun-gorsel-alani">
        ${src ? `<img class="ms-search-urun-gorsel" src="${src}" alt="${escHtml(t(p.nameI18n))}">` : ''}
      </span>
      <span class="ms-search-urun-icerik">
        <span class="ms-search-urun-baslik">${escHtml(t(p.nameI18n))}</span>
        <span class="ms-search-urun-fiyat ms-urun-fiyat">${fmt(price)}</span>
      </span>
    </a>`;
}

// ─────────────────────────────────────────────────────────
// UTILS
// ─────────────────────────────────────────────────────────
function normalizeList(data) {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if (Array.isArray(data.items)) return data.items;
  if (Array.isArray(data.categories)) return data.categories;
  if (Array.isArray(data.data)) return data.data;
  return [];
}

// ─────────────────────────────────────────────────────────
// ROUTER — History API
// ─────────────────────────────────────────────────────────
const router = {
  parsePath() {
    const segs = window.location.pathname.split('/').filter(Boolean);
    const params = {};
    for (const [k, v] of new URLSearchParams(window.location.search)) params[k] = v;
    return { segs, params };
  },

  async route() {
    const { segs, params } = this.parsePath();
    const page = segs[0] || 'home';

    // Sync search bar
    const si = qs('[data-ms-arama-input]');
    if (si && params.search) si.value = params.search;

    syncNavCats();
    window.msMobilMenuKapat?.();
    window.msSepetMenuKapat?.();
    window.scrollTo({ top: 0, behavior: 'smooth' });

    switch (page) {
      case 'home':
      case '':
        await pageHome(); break;
      case 'urunler':
        await pageProducts(params); break;
      case 'urun':
        if (segs[1]) await pageProduct(segs[1]);
        else         await pageProducts(params);
        break;
      default:
        // Tek segment → kategori slug olarak dene
        if (segs.length === 1) await pageCategory(segs[0], params);
        else await pageHome();
    }

    window.msRunPageModules(document);
  },

  init() {
    window.addEventListener('popstate', () => this.route());
    this.route();

    // Arama kutusunda Enter → tüm sonuçlar sayfasına git
    qsa('[data-ms-arama-input], [data-ms-arama-panel-input]').forEach(si => {
      si.addEventListener('keydown', e => {
        if (e.key === 'Enter' && si.value.trim())
          navigate('/urunler?search=' + encodeURIComponent(si.value.trim()));
      });
    });
  },
};

// ─────────────────────────────────────────────────────────
// LINK INTERCEPT — <a href="..."> tıklamalarını SPA'ya yönlendir
// ─────────────────────────────────────────────────────────
document.addEventListener('click', e => {
  const a = e.target.closest('a[href]');
  if (!a) return;
  const href = a.getAttribute('href');
  // Sadece kendi origin'imize ait, hash olmayan, gerçek navigasyon linkleri
  if (
    href &&
    !href.startsWith('http') &&
    !href.startsWith('//') &&
    !href.startsWith('mailto:') &&
    !href.startsWith('tel:') &&
    href !== '#'
  ) {
    e.preventDefault();
    navigate(href);
  }
});

// ─────────────────────────────────────────────────────────
// INIT
// ─────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  initNav();
  Cart.load();
  router.init();
});
