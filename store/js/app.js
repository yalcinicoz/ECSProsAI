/* ═══════════════════════════════════════════════════════════
   ECSPros Store — app.js
   Vanilla JS · Hash Router · No build step
═══════════════════════════════════════════════════════════ */

// ─────────────────────────────────────────────────────────
// CONFIG
// ─────────────────────────────────────────────────────────
const CFG = {
  API:    '/api',
  FPID:   '3c713ebc-0666-4d02-92ff-7ef4e701e5c1', // demo_web platform
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
  const d = document.createElement('div');
  d.className = 'toast ' + type;
  d.textContent = msg;
  wrap.appendChild(d);
  setTimeout(() => d.remove(), 3200);
}

function navigate(path) {
  window.location.hash = '#/' + path;
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

  products({ page = 1, pageSize = 24, search = '' } = {}) {
    const p = new URLSearchParams({ firmPlatformId: CFG.FPID, page, pageSize });
    if (search) p.set('search', search);
    return this._req(`/store/catalog/products?${p}`);
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
  const panel    = $('cartPanel');
  const backdrop = $('cartBackdrop');
  if (!panel) return;
  const open = panel.classList.contains('open');
  if (open) {
    panel.classList.remove('open');
    backdrop.classList.remove('open');
    document.body.style.overflow = '';
  } else {
    panel.classList.add('open');
    backdrop.classList.add('open');
    document.body.style.overflow = 'hidden';
    renderCartPanel();
  }
}

function renderCartPanel() {
  const body   = $('cartPanelBody');
  const footer = $('cartPanelFooter');
  const sub    = $('cartSubtotal');
  const total  = $('cartTotal');
  const cnt    = $('cartItemCount');
  if (!body) return;

  if (cnt) cnt.textContent = Cart.count ? `${Cart.count} ürün` : '';

  if (!Cart.items.length) {
    body.innerHTML = `
      <div class="cart-empty">
        <svg width="52" height="52" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1"><path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/><line x1="3" y1="6" x2="21" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>
        <p>Sepetiniz boş</p>
        <a href="#/products" onclick="toggleCart()" class="btn-ghost-sm">Alışverişe Başla</a>
      </div>`;
    if (footer) footer.style.display = 'none';
    return;
  }

  body.innerHTML = Cart.items.map(item => {
    const img = imgSrc(item.img)
      ? `<img class="cart-item-img" src="${imgSrc(item.img)}" alt="${escHtml(item.name)}" onerror="this.style.display='none'">`
      : `<div class="cart-item-img" style="background:var(--cream-deep)"></div>`;
    return `
      <div class="cart-item">
        ${img}
        <div class="cart-item-info">
          <div class="cart-item-name">${escHtml(item.name)}</div>
          ${item.sku ? `<div class="cart-item-sku">${escHtml(item.sku)}</div>` : ''}
          <div class="cart-item-bottom">
            <div class="cart-qty-ctrl">
              <button class="c-qty-btn" onclick="cartSetQty('${item.id}', ${item.qty - 1})">−</button>
              <span class="c-qty-n">${item.qty}</span>
              <button class="c-qty-btn" onclick="cartSetQty('${item.id}', ${item.qty + 1})">+</button>
            </div>
            <span class="cart-item-price">${fmt(item.price * item.qty)}</span>
          </div>
          <div style="margin-top:8px">
            <span class="cart-item-del" onclick="cartRemove('${item.id}')">Kaldır</span>
          </div>
        </div>
      </div>`;
  }).join('');

  if (footer) footer.style.display = 'block';
  if (sub)   sub.textContent   = fmt(Cart.total);
  if (total) total.textContent = fmt(Cart.total);
}

window.cartSetQty = async (itemId, newQty) => {
  if (newQty < 1) { await window.cartRemove(itemId); return; }
  try {
    await api.updateItem(Cart.id, itemId, newQty);
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
// COMPONENTS
// ─────────────────────────────────────────────────────────
function prodCardHtml(p, delay = 0) {
  const src = imgSrc(p.mainImageUrl);
  const name = t(p.nameI18n);
  const initial = name.charAt(0) || '?';
  const img = src
    ? `<img src="${src}" alt="${escHtml(name)}" loading="lazy" onerror="this.parentNode.innerHTML='<div class=\\'img-ph\\'>${escHtml(initial)}</div>'">`
    : `<div class="img-ph">${escHtml(initial)}</div>`;
  const price = p.minPrice ?? p.basePrice ?? 0;

  return `
    <div class="prod-card fade-up" style="animation-delay:${delay}ms" onclick="navigate('product/${escHtml(p.code)}')">
      <div class="prod-thumb">
        ${img}
        ${p.compareAtPrice ? '<span class="prod-badge">İndirim</span>' : ''}
        <div class="prod-quick">
          <button class="btn-quick" onclick="quickAdd(event,'${escHtml(p.code)}')">Sepete Ekle</button>
        </div>
      </div>
      <div class="prod-body">
        <div class="prod-name">${escHtml(name)}</div>
        <div class="prod-prices">
          <span class="price">${fmt(price)}</span>
          ${p.compareAtPrice ? `<span class="price-was">${fmt(p.compareAtPrice)}</span>` : ''}
        </div>
      </div>
    </div>`;
}

function skelGrid(n = 8) {
  return Array(n).fill(0).map(() => `
    <div class="skel-card">
      <div class="skel skel-thumb"></div>
      <div class="skel skel-line w60"></div>
      <div class="skel skel-line w35"></div>
    </div>`).join('');
}

// ─────────────────────────────────────────────────────────
// PAGES
// ─────────────────────────────────────────────────────────

// ── HOME ─────────────────────────────────────────────────
async function pageHome() {
  $('main').innerHTML = `
    <!-- HERO -->
    <section class="hero">
      <div class="hero-bg"></div>
      <div class="hero-content fade-up">
        <div class="hero-kicker">Koleksiyon 2025</div>
        <h1 class="hero-h1">Kaliteli Ürünler,<br><em>Uygun Fiyatlar.</em></h1>
        <p class="hero-lead">Binlerce ürün, güvenli ödeme, hızlı teslimat. Alışverişin keyfini yeniden keşfedin.</p>
        <div class="hero-ctas">
          <a href="#/products" class="btn-primary">Ürünleri Keşfet &nbsp;→</a>
          <a href="#/products" class="btn-outline-light">Kampanyalar</a>
        </div>
      </div>
      <div class="hero-figures">
        <div class="hero-fig">
          <div class="hero-fig-n">10K+</div>
          <div class="hero-fig-l">Ürün</div>
        </div>
        <div class="hero-fig">
          <div class="hero-fig-n">50K+</div>
          <div class="hero-fig-l">Müşteri</div>
        </div>
        <div class="hero-fig">
          <div class="hero-fig-n">%100</div>
          <div class="hero-fig-l">Güvenli</div>
        </div>
      </div>
    </section>

    <!-- CATEGORIES -->
    <section>
      <div class="wrap">
        <div class="sec-head">
          <div>
            <div class="sec-eyebrow">Koleksiyonlar</div>
            <h2 class="sec-title">Kategorileri Keşfet</h2>
          </div>
          <a href="#/products" class="sec-link">Tümünü Gör →</a>
        </div>
        <div class="cat-grid" id="homeCats">${Array(6).fill(0).map(() =>
          `<div class="cat-card"><div class="cat-art skel" style="animation:shimmer 1.5s infinite;background-size:200% 100%"></div></div>`
        ).join('')}</div>
      </div>
    </section>

    <!-- FEATURED PRODUCTS -->
    <section>
      <div class="wrap">
        <div class="sec-head">
          <div>
            <div class="sec-eyebrow">Öne Çıkanlar</div>
            <h2 class="sec-title">Popüler Ürünler</h2>
          </div>
          <a href="#/products" class="sec-link">Tüm Ürünler →</a>
        </div>
      </div>
      <div class="wrap" style="padding-bottom:0">
        <div class="prod-grid" id="homeFeat">${skelGrid(8)}</div>
      </div>
    </section>
  `;

  const [catRes, prodRes] = await Promise.allSettled([
    NAV.cats.length ? Promise.resolve(NAV.cats) : api.channelCategories(),
    api.products({ page: 1, pageSize: 8 }),
  ]);

  // Render channel category cards
  const catEl = $('homeCats');
  if (catEl) {
    if (catRes.status === 'fulfilled') {
      const allCats = Array.isArray(catRes.value) ? catRes.value : normalizeList(catRes.value);
      const roots = allCats.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder);
      const catGrads = [
        'linear-gradient(145deg,#1a0a2e 0%,#3d1a5e 100%)',
        'linear-gradient(145deg,#0a1628 0%,#1e3a6e 100%)',
        'linear-gradient(145deg,#1a2a0a 0%,#2e5a1a 100%)',
        'linear-gradient(145deg,#2a0a0a 0%,#6e1a1a 100%)',
        'linear-gradient(145deg,#0a2a2a 0%,#1a5e5e 100%)',
        'linear-gradient(145deg,#2a1a0a 0%,#6e3d0a 100%)',
        'linear-gradient(145deg,#1a0a1a 0%,#4e1a5e 100%)',
        'linear-gradient(145deg,#0a2a1a 0%,#1a5e3d 100%)',
      ];
      catEl.innerHTML = roots.length
        ? roots.slice(0, 8).map((c, i) => {
            const childCount = allCats.filter(x => x.parentId === c.id).length;
            const bg = c.displayImageUrl
              ? `background:url('${c.displayImageUrl}') center/cover`
              : `background:${catGrads[i % catGrads.length]}`;
            return `
            <div class="cat-card fade-up" style="animation-delay:${i * 40}ms"
                 onclick="navigate('category/${escHtml(c.slug)}')">
              <div class="cat-art" style="${bg}"></div>
              <div class="cat-veil"></div>
              <div class="cat-body">
                <div class="cat-label">${escHtml(t(c.nameI18n))}</div>
                ${childCount ? `<div class="cat-sub">${childCount} alt kategori</div>` : ''}
                ${c.badgeLabel ? `<span class="cat-badge">${escHtml(c.badgeLabel)}</span>` : ''}
              </div>
            </div>`;
          }).join('')
        : '<p style="color:var(--ink-40);font-size:13px;padding:12px 0">Henüz kategori yok.</p>';
    } else {
      catEl.innerHTML = '<p style="color:var(--ink-40);font-size:13px;padding:12px 0">Kategoriler yüklenemedi.</p>';
    }
  }

  // Render featured
  const featEl = $('homeFeat');
  if (featEl) {
    if (prodRes.status === 'fulfilled') {
      const items = (prodRes.value.items || []);
      featEl.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 35)).join('')
        : '<p style="color:var(--ink-40);font-size:13px;padding:24px;grid-column:1/-1">Ürün bulunamadı.</p>';
    } else {
      featEl.innerHTML = '<p style="color:var(--ink-40);font-size:13px;padding:24px;grid-column:1/-1">Ürünler yüklenemedi.</p>';
    }
  }
}

// ── PRODUCTS ─────────────────────────────────────────────
async function pageProducts({ page = 1, search = '', categoryId = null } = {}) {
  page = parseInt(page) || 1;

  const titleText = search
    ? `"${escHtml(search)}" Sonuçları`
    : categoryId ? 'Kategori Ürünleri' : 'Tüm Ürünler';

  $('main').innerHTML = `
    <div class="prods-page">
      <div class="prods-hero">
        <div class="prods-hero-inner">
          <div>
            <h1 class="fade-up">${titleText}</h1>
            <p class="fade-up fade-up-1">En iyi ürünleri keşfedin</p>
          </div>
          <span class="prods-count-badge" id="prodCountBadge"></span>
        </div>
      </div>

      <div class="toolbar">
        <div class="toolbar-inner">
          <div class="toolbar-search">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
            <input type="text" id="tbSearch" placeholder="Ürün ara…" value="${escHtml(search)}">
          </div>
          <div class="chips" id="catChips">
            <button class="chip ${!categoryId ? 'on' : ''}" onclick="navigate('products')">Tümü</button>
          </div>
        </div>
      </div>

      <div class="prods-body">
        <div class="result-info" id="resultInfo"></div>
        <div class="prod-grid" id="prodGrid">${skelGrid(12)}</div>
        <div id="paginator"></div>
      </div>
    </div>`;

  // Wire up toolbar search
  const tbSearch = $('tbSearch');
  if (tbSearch) {
    tbSearch.addEventListener('keydown', e => {
      if (e.key === 'Enter' && tbSearch.value.trim())
        navigate('products?search=' + encodeURIComponent(tbSearch.value.trim()));
    });
  }

  // Load channel category chips (non-blocking)
  const catLoader = NAV.cats.length ? Promise.resolve(NAV.cats) : api.channelCategories();
  catLoader.then(res => {
    const allCats = Array.isArray(res) ? res : normalizeList(res);
    const roots = allCats.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder);
    const chipsEl = $('catChips');
    if (chipsEl && roots.length) {
      const extra = roots.map(c => `
        <button class="chip" onclick="navigate('category/${escHtml(c.slug)}')">
          ${escHtml(t(c.nameI18n))}
        </button>`).join('');
      chipsEl.innerHTML = `<button class="chip on" onclick="navigate('products')">Tümü</button>${extra}`;
    }
  }).catch(() => {});

  // Load products
  try {
    const data = await api.products({ page, search, categoryId });
    const items = data.items || [];
    const total = data.totalCount ?? data.total ?? items.length;
    const ps    = data.pageSize  || 24;
    const totalPages = Math.ceil(total / ps);

    const info = $('resultInfo');
    if (info) info.textContent = `${total.toLocaleString('tr-TR')} ürün bulundu`;

    const badge = $('prodCountBadge');
    if (badge) badge.textContent = `${total.toLocaleString('tr-TR')} ürün`;

    const grid = $('prodGrid');
    if (grid) {
      grid.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 30)).join('')
        : `<p style="grid-column:1/-1;text-align:center;padding:60px 20px;color:var(--ink-40);font-size:14px">
             Ürün bulunamadı. <a href="#/products" style="color:var(--gold)">Tüm ürünlere dön</a>
           </p>`;
    }

    // Pagination
    if (totalPages > 1) {
      const pag = $('paginator');
      if (pag) pag.innerHTML = buildPagination(page, totalPages, { search, categoryId });
    }
  } catch (e) {
    const grid = $('prodGrid');
    if (grid) grid.innerHTML = `<p style="grid-column:1/-1;text-align:center;padding:60px 20px;color:var(--ink-40);font-size:14px">Hata: ${escHtml(e.message)}</p>`;
  }
}

// ── CATEGORY ─────────────────────────────────────────────
async function pageCategory(slug, { page = 1 } = {}) {
  page = parseInt(page) || 1;
  const cat = NAV.bySlug[slug];

  if (!cat) {
    setLoading();
    try {
      const allCats = normalizeList(await api.channelCategories());
      allCats.forEach(c => { NAV.bySlug[c.slug] = c; NAV.byId[c.id] = c; });
      NAV.cats = allCats;
      NAV.roots = allCats.filter(c => !c.parentId).sort((a, b) => a.sortOrder - b.sortOrder);
    } catch { $('main').innerHTML = '<p style="padding:80px 40px">Kategori yüklenemedi.</p>'; return; }
    return pageCategory(slug, { page });
  }

  const catName = t(cat.nameI18n);
  const childCats = NAV.cats.filter(c => c.parentId === cat.id).sort((a, b) => a.sortOrder - b.sortOrder);
  const parentCat = cat.parentId ? NAV.byId[cat.parentId] : null;

  $('main').innerHTML = `
    <div class="prods-page">
      <div class="prods-hero">
        <div class="prods-hero-inner">
          <div>
            ${parentCat ? `<div class="breadcrumb"><a href="#/category/${parentCat.slug}">${escHtml(t(parentCat.nameI18n))}</a> <span>›</span></div>` : ''}
            <h1 class="fade-up">${escHtml(catName)}</h1>
            <p class="fade-up fade-up-1">${cat.badgeLabel ? `<span class="cat-badge-inline">${escHtml(cat.badgeLabel)}</span>` : ''}</p>
          </div>
          <span class="prods-count-badge" id="prodCountBadge"></span>
        </div>
      </div>

      ${childCats.length ? `
      <div class="toolbar">
        <div class="toolbar-inner">
          <div class="chips">
            <button class="chip on" onclick="navigate('category/${slug}')">Tümü</button>
            ${childCats.map(c => `
              <button class="chip" onclick="navigate('category/${escHtml(c.slug)}')">
                ${escHtml(t(c.nameI18n))}
              </button>`).join('')}
          </div>
        </div>
      </div>` : ''}

      <div class="prods-body">
        <div class="result-info" id="resultInfo"></div>
        <div class="prod-grid" id="prodGrid">${skelGrid(12)}</div>
        <div id="paginator"></div>
      </div>
    </div>`;

  try {
    const data = await api.channelCategoryProducts(cat.id, { page, pageSize: 24 });
    const items = data.items || [];
    const total = data.totalCount ?? data.total ?? items.length;
    const ps    = data.pageSize || 24;
    const totalPages = Math.ceil(total / ps);

    const info = $('resultInfo');
    if (info) info.textContent = `${total.toLocaleString('tr-TR')} ürün bulundu`;

    const badge = $('prodCountBadge');
    if (badge) badge.textContent = `${total.toLocaleString('tr-TR')} ürün`;

    const grid = $('prodGrid');
    if (grid) {
      grid.innerHTML = items.length
        ? items.map((p, i) => prodCardHtml(p, i * 30)).join('')
        : `<p style="grid-column:1/-1;text-align:center;padding:60px 20px;color:var(--ink-40);font-size:14px">Bu kategoride henüz ürün bulunmuyor.</p>`;
    }

    if (totalPages > 1) {
      const pag = $('paginator');
      if (pag) pag.innerHTML = buildPagination(page, totalPages, {}, `category/${slug}`);
    }
  } catch (e) {
    const grid = $('prodGrid');
    if (grid) grid.innerHTML = `<p style="grid-column:1/-1;padding:60px 20px;text-align:center;color:var(--ink-40)">Ürünler yüklenemedi: ${escHtml(e.message)}</p>`;
  }
}

// ── PRODUCT DETAIL ────────────────────────────────────────
async function pageProduct(code) {
  $('main').innerHTML = `
    <div class="detail-wrap">
      <div class="gallery">
        <div class="gallery-main skel" style="aspect-ratio:4/5"></div>
      </div>
      <div class="detail-info">
        <div class="skel skel-line" style="width:55%;height:14px;margin:0 0 18px"></div>
        <div class="skel skel-line" style="width:85%;height:40px;margin:0 0 12px"></div>
        <div class="skel skel-line" style="width:40%;height:30px;margin:0 0 28px"></div>
        <div class="skel skel-line" style="width:100%;height:80px;margin:0 0 28px"></div>
      </div>
    </div>`;

  try {
    const product  = await api.product(code);
    const variants = (product.variants || []).filter(v => v.isActive);

    // Build attribute map: code → { name, values: [{id, name}] }
    const attrMap = {};
    for (const v of variants) {
      for (const a of (v.attributes || [])) {
        if (!attrMap[a.attributeTypeCode]) {
          attrMap[a.attributeTypeCode] = {
            code:     a.attributeTypeCode,
            nameI18n: a.attributeTypeNameI18n,
            values:   [],
          };
        }
        const exists = attrMap[a.attributeTypeCode].values.some(x => x.id === a.attributeValueId);
        if (!exists) attrMap[a.attributeTypeCode].values.push({
          id:      a.attributeValueId,
          nameI18n: a.attributeValueNameI18n,
        });
      }
    }
    const attrTypes = Object.values(attrMap);

    // Mutable state for the detail page
    const state = {
      product,
      variants,
      attrMap,
      attrTypes,
      selected: {},        // { attrTypeCode: valueId }
      variant: variants.length === 1 ? variants[0] : null,
      qty: 1,
    };

    // Expose for inline handlers
    window.__dp = state;

    function allImages() {
      if (state.variant) return (state.variant.images || []).sort((a,b) => a.sortOrder - b.sortOrder);
      return variants.flatMap(v => v.images || []).sort((a,b) => a.sortOrder - b.sortOrder);
    }

    function currentPrice() {
      if (state.variant) return state.variant.platformPrice ?? state.variant.basePrice;
      const prices = variants.map(v => v.platformPrice ?? v.basePrice).filter(Boolean);
      return prices.length ? Math.min(...prices) : null;
    }

    function currentCompare() {
      return state.variant?.compareAtPrice ?? null;
    }

    function render() {
      const imgs   = allImages();
      const main   = imgs[0];
      const price  = currentPrice();
      const cmp    = currentCompare();
      const disc   = cmp && cmp > price ? Math.round((1 - price / cmp) * 100) : 0;
      const canAdd = state.variant !== null || variants.length === 0;
      const desc   = t(product.shortDescriptionI18n);

      const imgMain = main
        ? `<img id="galMain" src="${imgSrc(main.imageUrl)}" alt="${escHtml(t(product.nameI18n))}"
              onerror="this.parentNode.innerHTML='<div class=\\'img-ph\\'>?</div>'">`
        : `<div class="img-ph">${escHtml(t(product.nameI18n).charAt(0) || '?')}</div>`;

      const thumbsHtml = imgs.length > 1
        ? `<div class="gallery-thumbs">
            ${imgs.map((img, i) => `
              <div class="g-thumb ${i === 0 ? 'on' : ''}"
                   onclick="dpThumb(this,'${escHtml(imgSrc(img.imageUrl))}')">
                <img src="${imgSrc(img.imageUrl)}" alt="" loading="lazy">
              </div>`).join('')}
           </div>`
        : '';

      const attrsHtml = attrTypes.map(at => {
        const curVal = state.selected[at.code];
        const curName = curVal
          ? escHtml(t(at.values.find(v => v.id === curVal)?.nameI18n) || '')
          : '';
        return `
          <div class="var-group">
            <div class="var-label">
              ${escHtml(t(at.nameI18n))}
              <span class="var-val" id="vval-${at.code}">${curName ? '— ' + curName : ''}</span>
            </div>
            <div class="var-opts">
              ${at.values.map(val => `
                <button class="var-opt ${state.selected[at.code] === val.id ? 'on' : ''}"
                        onclick="dpSelect('${at.code}','${val.id}',this)">
                  ${escHtml(t(val.nameI18n))}
                </button>`).join('')}
            </div>
          </div>`;
      }).join('');

      $('main').innerHTML = `
        <div class="detail-wrap fade-up">
          <div class="gallery">
            <div class="gallery-main">${imgMain}</div>
            ${thumbsHtml}
          </div>

          <div class="detail-info">
            <div class="breadcrumb">
              <a href="#/">Ana Sayfa</a>
              <span>›</span>
              <a href="#/products">Ürünler</a>
              <span>›</span>
              <span>${escHtml(t(product.nameI18n))}</span>
            </div>

            <h1 class="detail-name">${escHtml(t(product.nameI18n))}</h1>

            <div class="detail-prices">
              <span class="detail-price">${price != null ? fmt(price) : '—'}</span>
              ${cmp ? `<span class="detail-was">${fmt(cmp)}</span>` : ''}
              ${disc ? `<span class="detail-off">%${disc} İndirim</span>` : ''}
            </div>

            ${desc ? `<p class="detail-desc">${escHtml(desc)}</p>` : ''}

            ${attrsHtml}

            <div class="atc-row">
              <div class="qty">
                <button class="qty-btn" onclick="dpQty(-1)">−</button>
                <div class="qty-n" id="dpQtyN">1</div>
                <button class="qty-btn" onclick="dpQty(1)">+</button>
              </div>
              <button class="btn-atc" id="dpAtcBtn"
                      ${canAdd ? '' : 'disabled'}
                      onclick="dpAddToCart()">
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/><line x1="3" y1="6" x2="21" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>
                ${canAdd ? 'Sepete Ekle' : 'Varyant Seçin'}
              </button>
            </div>

            <div class="trust-row">
              <div class="trust-item"><span class="trust-icon">🚚</span><span>Ücretsiz kargo — 500₺ üzeri</span></div>
              <div class="trust-item"><span class="trust-icon">↩️</span><span>30 gün koşulsuz iade</span></div>
              <div class="trust-item"><span class="trust-icon">🔒</span><span>Güvenli 256-bit SSL ödeme</span></div>
            </div>
          </div>
        </div>`;
    }

    // Gallery thumbnail click
    window.dpThumb = (el, url) => {
      qsa('.g-thumb').forEach(t => t.classList.remove('on'));
      el.classList.add('on');
      const img = $('galMain');
      if (img) img.src = url;
    };

    // Attribute select
    window.dpSelect = (typeCode, valueId, btn) => {
      const s = window.__dp;
      s.selected[typeCode] = valueId;
      // Update label
      const labelEl = $(`vval-${typeCode}`);
      if (labelEl) {
        const at  = s.attrMap[typeCode];
        const val = at.values.find(v => v.id === valueId);
        labelEl.textContent = val ? '— ' + t(val.nameI18n) : '';
      }
      // Mark selected
      btn.closest('.var-opts').querySelectorAll('.var-opt').forEach(b => b.classList.remove('on'));
      btn.classList.add('on');
      // Find matching variant
      s.variant = s.variants.find(v =>
        Object.entries(s.selected).every(([code, id]) =>
          (v.attributes || []).some(a => a.attributeTypeCode === code && a.attributeValueId === id)
        )
      ) ?? null;
      // Update button
      const atcBtn = $('dpAtcBtn');
      if (atcBtn) {
        const canAdd = s.variant !== null;
        atcBtn.disabled = !canAdd;
        atcBtn.innerHTML = `
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/>
            <line x1="3" y1="6" x2="21" y2="6"/>
            <path d="M16 10a4 4 0 0 1-8 0"/>
          </svg>
          ${canAdd ? 'Sepete Ekle' : 'Varyant Seçin'}`;
      }
    };

    // Qty change
    window.dpQty = delta => {
      const s = window.__dp;
      s.qty = Math.max(1, s.qty + delta);
      const el = $('dpQtyN');
      if (el) el.textContent = s.qty;
    };

    // Add to cart
    window.dpAddToCart = async () => {
      const s   = window.__dp;
      const v   = s.variant ?? (s.variants.length === 1 ? s.variants[0] : null);
      if (!v) { toast('Lütfen varyant seçin.', 'err'); return; }
      const btn = $('dpAtcBtn');
      if (btn) { btn.disabled = true; btn.textContent = 'Ekleniyor…'; }
      const price = v.platformPrice ?? v.basePrice;
      await addToCart(v.id, s.qty, price);
      if (btn) {
        btn.disabled = false;
        btn.innerHTML = `
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/>
            <line x1="3" y1="6" x2="21" y2="6"/>
            <path d="M16 10a4 4 0 0 1-8 0"/>
          </svg>Sepete Ekle`;
      }
    };

    render();

  } catch (e) {
    $('main').innerHTML = `
      <div style="text-align:center;padding:100px 40px;color:var(--ink-40)">
        <div style="font-family:var(--font-disp);font-size:64px;margin-bottom:16px;opacity:.3">?</div>
        <h2 style="font-family:var(--font-disp);font-size:28px;margin-bottom:10px;color:var(--ink)">Ürün Bulunamadı</h2>
        <p style="font-size:14px;margin-bottom:28px">${escHtml(e.message)}</p>
        <a href="#/products" style="color:var(--gold);font-size:12px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;border-bottom:1px solid currentColor;padding-bottom:2px">← Ürünlere Dön</a>
      </div>`;
  }
}

// ─────────────────────────────────────────────────────────
// QUICK ADD (from product grid)
// ─────────────────────────────────────────────────────────
window.quickAdd = async (event, code) => {
  event.stopPropagation();
  const btn = event.currentTarget;
  const orig = btn.textContent;
  btn.textContent = '…'; btn.disabled = true;
  try {
    const p = await api.product(code);
    const vs = (p.variants || []).filter(v => v.isActive);
    if (!vs.length) { toast('Stok bulunamadı.', 'err'); return; }
    if (vs.length === 1 || !vs[0].attributes?.length) {
      await addToCart(vs[0].id, 1, vs[0].platformPrice ?? vs[0].basePrice);
    } else {
      navigate('product/' + code);
    }
  } catch (e) { toast('Hata: ' + e.message, 'err'); }
  finally { btn.textContent = orig; btn.disabled = false; }
};

// ─────────────────────────────────────────────────────────
// PAGINATION
// ─────────────────────────────────────────────────────────
function buildPagination(current, total, params = {}, basePath = 'products') {
  function pgLink(p, label, disabled = false, active = false) {
    const q = new URLSearchParams({ ...params, page: p }).toString();
    const dest = basePath + (q ? '?' + q : '');
    return `<button class="pg-btn${active ? ' on' : ''}"
               ${disabled ? 'disabled' : ''}
               onclick="navigate('${escHtml(dest)}')">${label}</button>`;
  }

  const pages = [];
  pages.push(pgLink(current - 1, '←', current === 1));

  let range;
  if (total <= 7) {
    range = Array.from({ length: total }, (_, i) => i + 1);
  } else if (current <= 4) {
    range = [1, 2, 3, 4, 5, '…', total];
  } else if (current >= total - 3) {
    range = [1, '…', total-4, total-3, total-2, total-1, total];
  } else {
    range = [1, '…', current-1, current, current+1, '…', total];
  }

  for (const p of range) {
    if (p === '…') pages.push(`<span class="pg-dots">…</span>`);
    else pages.push(pgLink(p, p, false, p === current));
  }
  pages.push(pgLink(current + 1, '→', current === total));

  return `<div class="pagination">${pages.join('')}</div>`;
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

    // Build cat-strip with mega-drop dropdowns
    const strip = $('catStripInner');
    if (strip) {
      const rootsHtml = NAV.roots.map(c => {
        const subs = allCats.filter(s => s.parentId === c.id).sort((a, b) => a.sortOrder - b.sortOrder);
        const hasSub = subs.length > 0;
        const subHtml = hasSub ? `
          <div class="mega-drop">
            ${subs.map(s => `
              <a href="#/category/${s.slug}" class="mega-drop-item" data-href="#/category/${s.slug}">
                ${escHtml(t(s.nameI18n))}
              </a>`).join('')}
          </div>` : '';
        return `
          <div class="cat-item">
            <a href="#/category/${c.slug}" class="cat-chip${hasSub ? ' has-sub' : ''}"
               data-href="#/category/${c.slug}">
              ${escHtml(t(c.nameI18n))}${hasSub ? '<svg class="sub-arr" viewBox="0 0 10 6" width="8" height="8"><path d="M1 1l4 4 4-4" stroke="currentColor" stroke-width="1.5" fill="none" stroke-linecap="round"/></svg>' : ''}
            </a>
            ${subHtml}
          </div>`;
      }).join('');

      strip.innerHTML = `
        <div class="cat-item">
          <a href="#/products" class="cat-chip" data-href="#/products">Tüm Ürünler</a>
        </div>
        ${rootsHtml}`;
    }

    // Footer categories
    const footerCats = $('footerCats');
    if (footerCats) {
      footerCats.innerHTML = NAV.roots.map(c => `
        <a href="#/category/${c.slug}">${escHtml(t(c.nameI18n))}</a>`).join('');
    }
  } catch(e) {
    console.error('Nav init failed:', e);
  }
}

// Mark active nav chip
function syncNavCats() {
  const hash = window.location.hash.replace(/\?.*$/, '');
  qsa('.cat-chip[data-href], .mega-drop-item[data-href]').forEach(a => {
    const href = a.dataset.href?.replace(/\?.*$/, '');
    a.classList.toggle('active', href === hash);
  });
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
// ROUTER
// ─────────────────────────────────────────────────────────
const router = {
  parseHash() {
    const raw   = window.location.hash.replace(/^#\/?/, '') || '';
    const [pathPart, qsPart] = raw.split('?');
    const segs  = pathPart.split('/').filter(Boolean);
    const params = {};
    if (qsPart) for (const [k, v] of new URLSearchParams(qsPart)) params[k] = v;
    return { segs, params };
  },

  async route() {
    const { segs, params } = this.parseHash();
    const page = segs[0] || 'home';

    // Sync search bar
    const si = $('searchInput');
    if (si && params.search) si.value = params.search;

    syncNavCats();
    window.scrollTo({ top: 0, behavior: 'smooth' });

    switch (page) {
      case '':
      case 'home':
        await pageHome(); break;
      case 'products':
        await pageProducts(params); break;
      case 'product':
        if (segs[1]) await pageProduct(segs[1]);
        else         await pageProducts(params);
        break;
      case 'category':
        if (segs[1]) await pageCategory(segs[1], params);
        else         await pageHome();
        break;
      default:
        await pageHome();
    }
  },

  init() {
    window.addEventListener('hashchange', () => this.route());
    this.route();

    // Search on enter
    const si = $('searchInput');
    if (si) {
      si.addEventListener('keydown', e => {
        if (e.key === 'Enter' && si.value.trim())
          navigate('products?search=' + encodeURIComponent(si.value.trim()));
      });
    }
  },
};

// ─────────────────────────────────────────────────────────
// INIT
// ─────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  // Navbar scroll effect
  window.addEventListener('scroll', () => {
    const nav = $('navbar');
    if (nav) nav.classList.toggle('scrolled', window.scrollY > 10);
  }, { passive: true });

  // Boot
  initNav();
  Cart.load();
  router.init();
});
