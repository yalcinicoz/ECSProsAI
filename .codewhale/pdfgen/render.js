const fs = require('fs');
const PDFDocument = require('pdfkit');

const SRC = '/opt/ECSProsAI/docs/AIAnalizRaporlari/ECSPROS_DS_Degerlendirme.md';
const OUT = '/opt/ECSProsAI/docs/AIAnalizRaporlari/ECSPROS_DS_Degerlendirme.pdf';

const md = fs.readFileSync(SRC, 'utf8');

// ---------- Fonts ----------
const FONT_DIR = '/usr/share/fonts/truetype/dejavu/';
const F = { body: 'Body', bold: 'Bold', mono: 'Mono' };

// ---------- Page geometry ----------
const PAGE_W = 595.28, PAGE_H = 841.89;
const ML = 56, MR = 56, MT = 64, MB = 24; // MB = pdfkit alt kenar boşluğu
const CONTENT_W = PAGE_W - ML - MR;
const BOTTOM = PAGE_H - 64;   // içerik alt sınırı (footer'a yer bırakır)
const FOOTER_Y = PAGE_H - 40; // alt bilgi satırı

// ---------- Colors ----------
const C = {
  ink: '#1f2733',
  muted: '#5b6673',
  accent: '#14538c',
  accentDark: '#0e3a63',
  code: '#8a2e4d',
  codeBg: '#f3f1f5',
  rule: '#d7dce2',
  quoteBar: '#14538c',
  quoteBg: '#f4f7fa',
  tableHeadBg: '#e8eef5',
  tableLine: '#c9d2dc',
  link: '#14538c',
};

// ---------- Parser: markdown -> blocks ----------
function parseBlocks(md) {
  const lines = md.replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let i = 0;
  function isBlank(s) { return /^\s*$/.test(s); }
  function isHr(s) { return /^\s*---+\s*$/.test(s); }
  function isHeading(s) { return s.match(/^(#{1,6})\s+(.*)$/); }
  function isBullet(s) { return s.match(/^\s*[-*]\s+(.*)$/); }
  function isOrdered(s) { return s.match(/^\s*(\d+)\.\s+(.*)$/); }
  function isTable(s) { return /^\s*\|.*\|\s*$/.test(s); }
  function isQuote(s) { return s.match(/^\s*>\s?(.*)$/); }
  function indentLevel(s) { const m = s.match(/^\s*/); return m ? m[0].length : 0; }

  while (i < lines.length) {
    const line = lines[i];
    if (isBlank(line)) { i++; continue; }
    if (isHr(line)) { blocks.push({ type: 'hr' }); i++; continue; }

    let m;
    if ((m = isHeading(line))) {
      blocks.push({ type: 'heading', level: m[1].length, text: m[2].trim() });
      i++; continue;
    }
    if ((m = isTable(line))) {
      // collect table block (header + separator + rows)
      const rows = [];
      let j = i;
      while (j < lines.length && isTable(lines[j])) {
        const cells = lines[j].trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(c => c.trim());
        rows.push(cells);
        j++;
      }
      // drop the separator row (|---|)
      const header = rows[0] || [];
      const data = rows.slice(1).filter(r => !r.every(c => /^:?-+:?$/.test(c)));
      blocks.push({ type: 'table', header, rows: data });
      i = j; continue;
    }
    if ((m = isQuote(line))) {
      const qlines = [m[1].trim()];
      i++;
      while (i < lines.length && isQuote(lines[i])) {
        qlines.push(isQuote(lines[i])[1].trim());
        i++;
      }
      blocks.push({ type: 'quote', lines: qlines });
      continue;
    }
    if (isBullet(line) || isOrdered(line)) {
      const ordered = !!isOrdered(line);
      const items = [];
      let j = i;
      while (j < lines.length) {
        const l = lines[j];
        let bm = isBullet(l), om = isOrdered(l);
        if (bm || om) {
          const cont = [];
          j++;
          // continuation: indented lines until blank or a new block marker
          while (j < lines.length && !isBlank(lines[j]) && indentLevel(lines[j]) >= 2 &&
                 !isHeading(lines[j]) && !isHr(lines[j]) && !isBullet(lines[j]) && !isOrdered(lines[j]) && !isTable(lines[j]) && !isQuote(lines[j])) {
            cont.push(lines[j].trim());
            j++;
          }
          items.push({ text: (bm ? bm[1] : om[2]).trim(), cont });
        } else {
          break;
        }
      }
      blocks.push({ type: 'list', ordered, items });
      i = j; continue;
    }

    // paragraph (gather consecutive non-blank, non-block lines)
    const plines = [line.trim()];
    i++;
    while (i < lines.length && !isBlank(lines[i]) && !isHeading(lines[i]) && !isHr(lines[i]) &&
           !isBullet(lines[i]) && !isOrdered(lines[i]) && !isTable(lines[i]) && !isQuote(lines[i])) {
      plines.push(lines[i].trim());
      i++;
    }
    blocks.push({ type: 'para', text: plines.join(' ') });
  }
  return blocks;
}

// ---------- Inline tokenizer ----------
function tokenize(text) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push({ t: text.slice(last, m.index), bold: false, code: false });
    const s = m[0];
    if (s.startsWith('**')) out.push({ t: s.slice(2, -2), bold: true, code: false });
    else out.push({ t: s.slice(1, -1), bold: false, code: true });
    last = m.index + s.length;
  }
  if (last < text.length) out.push({ t: text.slice(last), bold: false, code: false });
  return out;
}

// ---------- PDF document ----------
const doc = new PDFDocument({
  size: 'A4',
  margins: { top: MT, bottom: MB, left: ML, right: MR },
  bufferPages: false,
  info: {
    Title: 'ECSPros — Dayanıklılık, Performans ve Ölçeklenebilirlik Değerlendirmesi',
    Author: 'ECSPros',
    Subject: 'Statik kod analizi ve yapılandırma incelemesi',
    CreationDate: new Date(),
  },
});
doc.registerFont(F.body, FONT_DIR + 'DejaVuSans.ttf');
doc.registerFont(F.bold, FONT_DIR + 'DejaVuSans-Bold.ttf');
doc.registerFont(F.mono, FONT_DIR + 'DejaVuSansMono.ttf');

const stream = fs.createWriteStream(OUT);
doc.pipe(stream);

let y = MT;
let pageNo = 1;

function fontFor(tok) { return tok.code ? F.mono : (tok.bold ? F.bold : F.body); }
function widthOf(str, name, size) { doc.font(name).fontSize(size); return doc.widthOfString(str); }

function ensure(h) {
  if (y + h > BOTTOM) newPage();
}
function newPage() {
  drawFooter();
  doc.addPage();
  pageNo++;
  y = MT;
}
function drawFooter() {
  doc.save();
  doc.rect(ML, FOOTER_Y - 10, CONTENT_W, 0.5).fill(C.rule);
  doc.font(F.body).fontSize(7.5).fillColor(C.muted);
  doc.text('ECSPros — Dayanıklılık, Performans ve Ölçeklenebilirlik Değerlendirmesi', ML, FOOTER_Y, { lineBreak: false });
  const pn = String(pageNo);
  doc.text(pn, PAGE_W - MR - doc.widthOfString(pn), FOOTER_Y, { lineBreak: false });
  doc.restore();
}

// ---------- Rich paragraph layout ----------
function tokensToWords(segs) {
  const words = [];
  for (const seg of segs) {
    const parts = seg.t.split(/(\s+)/);
    for (const p of parts) {
      if (p === '') continue;
      const space = /^\s+$/.test(p);
      words.push({ t: space ? ' ' : p, bold: seg.bold, code: seg.code, space });
    }
  }
  return words;
}

function layoutWords(words, size, maxW) {
  const lines = [];
  let cur = [], curW = 0;
  for (const w of words) {
    if (w.space) {
      if (cur.length) {
        const sw = widthOf(' ', fontFor(w), size);
        curW += sw;
        cur.push(w);
      }
      continue;
    }
    const ww = widthOf(w.t, fontFor(w), size);
    if (cur.length && curW + ww > maxW) { lines.push(cur); cur = []; curW = 0; }
    cur.push(w); curW += ww;
  }
  if (cur.length) lines.push(cur);
  return lines;
}

function drawRichLine(line, x, yy, size) {
  let cx = x;
  for (const tok of line) {
    const name = fontFor(tok);
    doc.font(name).fontSize(size);
    if (tok.space) { cx += doc.widthOfString(' '); continue; }
    doc.fillColor(tok.code ? C.code : C.ink);
    doc.text(tok.t, cx, yy, { lineBreak: false });
    cx += doc.widthOfString(tok.t);
  }
}

function renderRich(text, x, maxW, size, opts) {
  opts = opts || {};
  const lh = opts.lineHeight || (size * 1.5);
  const segs = tokenize(text);
  const words = tokensToWords(segs);
  const lines = layoutWords(words, size, maxW);
  for (const ln of lines) {
    drawRichLine(ln, x, y, size);
    y += lh;
  }
  return y;
}

// ---------- Block renderers ----------
function renderHeading(text, level) {
  if (level === 1) {
    ensure(96);
    doc.font(F.bold).fontSize(21).fillColor(C.accentDark);
    const th = doc.heightOfString(text, { width: CONTENT_W });
    doc.text(text, ML, y, { width: CONTENT_W });
    y += th + 8;
    doc.font(F.body).fontSize(9.5).fillColor(C.muted);
    doc.text('15 Ağustos 2026  ·  Statik kod analizi + yapılandırma incelemesi', ML, y, { width: CONTENT_W });
    y += 16;
    doc.rect(ML, y, CONTENT_W, 2).fill(C.accent);
    y += 22;
  } else if (level === 2) {
    ensure(42);
    y += 8;
    doc.font(F.bold).fontSize(13.5).fillColor(C.accentDark);
    const th = doc.heightOfString(text, { width: CONTENT_W });
    doc.text(text, ML, y, { width: CONTENT_W });
    y += th + 8;
    doc.rect(ML, y, CONTENT_W, 0.8).fill(C.rule);
    y += 12;
  } else {
    ensure(30);
    y += 5;
    doc.font(F.bold).fontSize(11).fillColor(C.accent);
    const th = doc.heightOfString(text, { width: CONTENT_W });
    doc.text(text, ML, y, { width: CONTENT_W });
    y += th + 7;
  }
}

function renderPara(text) {
  renderRich(text, ML, CONTENT_W, 9.5, {});
  y += 7;
}

function renderQuote(lines) {
  const text = lines.join(' ');
  const pad = 9;
  const x = ML + 6;
  const w = CONTENT_W - 12;
  const segs = tokenize(text);
  const words = tokensToWords(segs);
  const laid = layoutWords(words, 9.3, w - pad * 2);
  const h = laid.length * 14 + pad * 2;
  ensure(h);
  doc.save();
  doc.rect(ML, y, CONTENT_W, h).fill(C.quoteBg);
  doc.rect(ML, y, 3, h).fill(C.quoteBar);
  doc.restore();
  y = y + pad;
  renderRich(text, x + pad, w - pad * 2, 9.3, { lineHeight: 14 });
  y = y + pad + 6;
}

function renderList(block) {
  const markerW = block.ordered ? 26 : 14;
  const textX = ML + markerW;
  const textW = CONTENT_W - markerW;
  block.items.forEach((item, idx) => {
    ensure(14);
    const marker = block.ordered ? `${idx + 1}.` : '•';
    doc.save();
    doc.font(F.bold).fontSize(9.5).fillColor(C.accent);
    doc.text(marker, ML, y, { lineBreak: false, width: markerW });
    doc.restore();
    renderRich(item.text, textX, textW, 9.5, {});
    for (const c of item.cont) {
      renderRich(c, textX + 4, textW - 4, 9.3, {});
    }
    y += 2.5;
  });
  y += 4;
}

function stripInline(s) {
  return s.replace(/\*\*/g, '').replace(/`/g, '').trim();
}

function renderTable(block) {
  const header = block.header.map(stripInline);
  const rows = block.rows.map(r => r.map(stripInline));
  const ncol = header.length || (rows[0] ? rows[0].length : 1);
  // column widths: first narrow, rest distributed
  const colW = [];
  for (let c = 0; c < ncol; c++) colW.push(c === 0 ? 28 : (CONTENT_W - 28) / (ncol - 1));
  const cellPad = 5;
  const size = 8.2;
  const lh = 10.6;

  function cellLines(text, w) {
    const words = text.split(/\s+/).filter(Boolean);
    const lines = [];
    let cur = '';
    const avail = w - cellPad * 2;
    for (const wd of words) {
      const candidate = cur ? cur + ' ' + wd : wd;
      if (cur && widthOf(candidate, F.body, size) > avail) { lines.push(cur); cur = wd; }
      else cur = candidate;
    }
    if (cur) lines.push(cur);
    return lines.length ? lines : [''];
  }

  function rowHeight(cells) {
    let max = 1;
    for (let c = 0; c < ncol; c++) max = Math.max(max, cellLines(cells[c] || '', colW[c]).length);
    return max * lh + cellPad * 2;
  }

  const drawRow = (cells, isHead) => {
    const h = rowHeight(cells);
    ensure(h);
    let x = ML;
    doc.save();
    for (let c = 0; c < ncol; c++) {
      const w = colW[c];
      if (isHead) { doc.rect(x, y, w, h).fill(C.tableHeadBg); }
      doc.rect(x, y, w, h).stroke(C.tableLine);
      x += w;
    }
    x = ML;
    for (let c = 0; c < ncol; c++) {
      const w = colW[c];
      const lines = cellLines(cells[c] || '', w);
      doc.font(isHead ? F.bold : F.body).fontSize(size).fillColor(isHead ? C.accentDark : C.ink);
      lines.forEach((ln, li) => {
        doc.text(ln, x + cellPad, y + cellPad + li * lh, { lineBreak: false, width: w - cellPad * 2 });
      });
      x += w;
    }
    doc.restore();
    y += h;
  };

  drawRow(header, true);
  rows.forEach(r => drawRow(r, false));
  y += 6;
}

// ---------- Main render loop ----------
const blocks = parseBlocks(md);

for (const b of blocks) {
  switch (b.type) {
    case 'heading': renderHeading(b.text, b.level); break;
    case 'para': renderPara(b.text); break;
    case 'quote': renderQuote(b.lines); break;
    case 'list': renderList(b); break;
    case 'table': renderTable(b); break;
    case 'hr': y += 6; doc.save(); doc.rect(ML, y, CONTENT_W, 0.6).fill(C.rule); y += 10; doc.restore(); break;
  }
}

drawFooter();
doc.end();

stream.on('finish', () => {
  const sz = fs.statSync(OUT).size;
  console.log('PDF üretildi:', OUT, '(' + sz + ' bayt,', pageNo, 'sayfa)');
});
stream.on('error', (e) => { console.error('HATA:', e); process.exit(1); });
