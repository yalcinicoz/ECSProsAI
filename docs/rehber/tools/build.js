// ECSPros Admin Rehberi derleyicisi — docs/rehber/content/**.md → docker/nginx/html/rehber/ (çok sayfalı statik site)
import { marked } from 'marked';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const CONTENT = path.join(ROOT, 'content');
const IMG = path.join(ROOT, 'img');
const OUT = path.resolve(ROOT, '../../docker/nginx/html/rehber');
const BASE = '/rehber/';

const slug = (s) => s.toLowerCase()
  .replace(/ğ/g, 'g').replace(/ü/g, 'u').replace(/ş/g, 's').replace(/ı/g, 'i').replace(/ö/g, 'o').replace(/ç/g, 'c')
  .replace(/&/g, 've').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

function frontMatter(md) {
  const m = md.match(/^---\n([\s\S]*?)\n---\n?/);
  const meta = {};
  if (!m) return { meta, body: md };
  for (const line of m[1].split('\n')) {
    const i = line.indexOf(':'); if (i < 0) continue;
    meta[line.slice(0, i).trim()] = line.slice(i + 1).trim().replace(/^['"]|['"]$/g, '');
  }
  return { meta, body: md.slice(m[0].length) };
}

// Blok uyarıları / görsel / başlık özelleştirmeleri (marked v15: renderer metodları this.parser kullanır)
const renderer = {
  blockquote(token) {
    const inner = this.parser.parse(token.tokens);
    const m = inner.match(/<strong>(İpucu|Dikkat|Not|Uyarı)\s*:?<\/strong>/);
    const tip = !m ? '' : m[1] === 'İpucu' ? ' callout ipucu' : m[1] === 'Not' ? ' callout not' : ' callout dikkat';
    return `<blockquote class="${tip.trim()}">\n${inner}</blockquote>\n`;
  },
  image(token) {
    // img/<slug>.webp yoksa .png'ye düş (ekran görüntüsü aracı webp üretemediyse)
    let rel = token.href;
    if (rel.startsWith('img/') && !fs.existsSync(path.join(IMG, rel.slice(4))) && rel.endsWith('.webp')
        && fs.existsSync(path.join(IMG, rel.slice(4).replace(/\.webp$/, '.png'))))
      rel = rel.replace(/\.webp$/, '.png');
    const href = rel.startsWith('img/') ? BASE + rel : rel;
    const alt = (token.text || '').replace(/"/g, '&quot;');
    return `<figure class="ekran"><a href="${href}" class="buyut" data-alt="${alt}"><img src="${href}" alt="${alt}" loading="lazy" onerror="this.closest('figure').classList.add('eksik')"></a><figcaption>${alt}</figcaption></figure>`;
  },
  heading(token) {
    const text = this.parser.parseInline(token.tokens);
    const id = slug(token.text.replace(/<[^>]+>/g, ''));
    return `<h${token.depth} id="${id}">${text}<a class="anchor" href="#${id}" aria-hidden="true">#</a></h${token.depth}>\n`;
  }
};
marked.use({ renderer, gfm: true, breaks: false });

// İçerik ağacı
const groups = [];
for (const gdir of fs.readdirSync(CONTENT).sort()) {
  const gpath = path.join(CONTENT, gdir);
  if (!fs.statSync(gpath).isDirectory()) continue;
  const gm = gdir.match(/^(\d+)-(.+)$/);
  const group = { dir: gdir, order: gm ? Number(gm[1]) : 999, slug: gm ? gm[2] : slug(gdir), title: null, pages: [] };
  for (const f of fs.readdirSync(gpath).filter(f => f.endsWith('.md')).sort()) {
    const raw = fs.readFileSync(path.join(gpath, f), 'utf8');
    const { meta, body } = frontMatter(raw);
    const pslug = f.replace(/\.md$/, '').replace(/^\d+-/, '');
    const page = {
      file: f, slug: pslug, title: meta.title || pslug, route: meta.route || '', summary: meta.summary || '',
      order: Number(meta.order || 999), groupTitle: meta.group || group.slug, body,
      html: marked.parse(body),
      headings: [...body.matchAll(/^##\s+(.+)$/gm)].map(m => m[1].trim()),
      text: body.replace(/[#>*`|\[\]()!-]/g, ' ').replace(/\s+/g, ' ').slice(0, 4000),
    };
    if (!group.title) group.title = meta.group || group.slug;
    group.pages.push(page);
  }
  group.pages.sort((a, b) => a.order - b.order || a.title.localeCompare(b.title, 'tr'));
  if (group.pages.length) groups.push(group);
}
groups.sort((a, b) => a.order - b.order);
const flat = groups.flatMap(g => g.pages.map(p => ({ g, p })));

const css = fs.readFileSync(path.join(__dirname, 'site.css'), 'utf8');
const js = fs.readFileSync(path.join(__dirname, 'site.js'), 'utf8');
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');

function nav(activeG, activeP) {
  return groups.map(g => `<div class="group">${esc(g.title)}</div>` + g.pages.map(p =>
    `<a href="${BASE}${g.slug}/${p.slug}/" class="${g === activeG && p === activeP ? 'active' : ''}">${esc(p.title)}</a>`).join('')).join('');
}
function layout({ title, bodyHtml, activeG, activeP, crumbs }) {
  return `<!DOCTYPE html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title)} — ECSPros Admin Paneli Kullanım Rehberi</title><meta name="robots" content="noindex,nofollow">
<link rel="stylesheet" href="${BASE}site.css"></head><body><div class="layout">
<aside><h1><a href="${BASE}">ECSPros Admin Paneli</a></h1><p class="sub">Kullanım Rehberi</p>
<div class="arama"><input type="search" id="arama" placeholder="Rehberde ara… (sayfa, buton, alan)" autocomplete="off"><div id="arama-sonuc" class="arama-sonuc" hidden></div></div>
<nav>${nav(activeG, activeP)}</nav></aside>
<main><div class="crumbs">${crumbs}</div>${bodyHtml}</main></div>
<div id="lightbox" hidden><img alt=""><div class="lb-alt"></div></div>
<script>window.__REHBER_BASE='${BASE}';${js}</script></body></html>`;
}

fs.rmSync(OUT, { recursive: true, force: true });
fs.mkdirSync(OUT, { recursive: true });
fs.writeFileSync(path.join(OUT, 'site.css'), css);
if (fs.existsSync(IMG)) fs.cpSync(IMG, path.join(OUT, 'img'), { recursive: true });

// Sayfalar
flat.forEach(({ g, p }, i) => {
  const prev = flat[i - 1], next = flat[i + 1];
  const toc = p.headings.length ? `<div class="toc"><b>Bu sayfada</b>${p.headings.map(h => `<a href="#${slug(h)}">${esc(h)}</a>`).join('')}</div>` : '';
  const body = `<article><header class="sayfa-baslik"><div class="grup-etiketi">${esc(g.title)}</div><h1>${esc(p.title)}</h1>
${p.summary ? `<p class="ozet">${esc(p.summary)}</p>` : ''}${p.route ? `<p class="rota">Panel adresi: <code>/admin${esc(p.route)}</code></p>` : ''}</header>
${toc}${p.html}
<nav class="oncesonra">${prev ? `<a class="onceki" href="${BASE}${prev.g.slug}/${prev.p.slug}/">← ${esc(prev.p.title)}</a>` : '<span></span>'}${next ? `<a class="sonraki" href="${BASE}${next.g.slug}/${next.p.slug}/">${esc(next.p.title)} →</a>` : ''}</nav></article>`;
  const dir = path.join(OUT, g.slug, p.slug); fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'index.html'), layout({ title: p.title, bodyHtml: body, activeG: g, activeP: p,
    crumbs: `<a href="${BASE}">Rehber</a> › <span>${esc(g.title)}</span> › <b>${esc(p.title)}</b>` }));
});

// Ana sayfa
const kartlar = groups.map(g => `<section class="bolum"><h2 id="${g.slug}">${esc(g.title)}</h2><div class="kartlar">${g.pages.map(p =>
  `<a class="kart" href="${BASE}${g.slug}/${p.slug}/"><strong>${esc(p.title)}</strong><span>${esc(p.summary || '')}</span></a>`).join('')}</div></section>`).join('');
const giris = fs.existsSync(path.join(ROOT, 'index.md')) ? marked.parse(frontMatter(fs.readFileSync(path.join(ROOT, 'index.md'), 'utf8')).body) : '';
fs.writeFileSync(path.join(OUT, 'index.html'), layout({ title: 'Giriş', activeG: null, activeP: null, crumbs: `<b>Rehber</b>`,
  bodyHtml: `<header class="hero"><h1>ECSPros Admin Paneli — Kullanım Rehberi</h1><p>Tüm mağaza ve kanalların yönetim panelleri için ortak rehber. Soldaki menüden ya da aramadan bir sayfa seçin.</p></header>${giris}${kartlar}` }));

// Arama indeksi
fs.writeFileSync(path.join(OUT, 'arama.json'), JSON.stringify(flat.map(({ g, p }) => ({
  t: p.title, g: g.title, u: `${BASE}${g.slug}/${p.slug}/`, h: p.headings, s: p.summary, x: p.text }))));
// İç bağlantı kontrolü (uyarı)
const urls = new Set(flat.map(({ g, p }) => `${BASE}${g.slug}/${p.slug}/`)); urls.add(BASE);
for (const { g, p } of flat) for (const m of p.body.matchAll(/\]\((\/rehber\/[^)#\s]*)/g))
  if (!urls.has(m[1])) console.warn(`⚠ kırık bağlantı: ${g.dir}/${p.file} → ${m[1]}`);
console.log(`✓ Rehber derlendi: ${groups.length} bölüm, ${flat.length} sayfa → ${OUT}`);
