import fs from "node:fs";
import path from "node:path";

const projeKoku = process.cwd();
const kaynakCssYolu = path.join(projeKoku, "wwwroot", "fontawesome-free-7.2.0-web", "css", "all.min.css");
const hedefCssYolu = path.join(projeKoku, "wwwroot", "fontawesome-free-7.2.0-web", "css", "ikonall.min.css");
const taramaKokleri = [
  path.join(projeKoku, "Views"),
  path.join(projeKoku, "wwwroot", "js", "site.js")
];

const kaynakUzantilari = new Set([".cshtml", ".js"]);
const stilSiniflari = new Set([
  "fa",
  "fas",
  "far",
  "fab",
  "fa-solid",
  "fa-regular",
  "fa-brands",
  "fa-classic",
  "fa-ikon"
]);

const yardimciSinifDesenleri = [
  /^fa-\d+x$/,
  /^fa-(2xs|xs|sm|lg|xl|2xl)$/,
  /^fa-(fw|width-.+|ul|li|border|pull-.+|beat|bounce|fade|beat-fade|flip|shake|spin|spin-reverse|pulse|spin-pulse|rotate-.+|flip-.+|stack|stack-.+|inverse)$/
];

function dosyalariGez(kok) {
  if (!fs.existsSync(kok)) {
    return [];
  }

  const durum = fs.statSync(kok);
  if (durum.isFile()) {
    return kaynakUzantilari.has(path.extname(kok)) ? [kok] : [];
  }

  return fs.readdirSync(kok).flatMap((ad) => dosyalariGez(path.join(kok, ad)));
}

function ikonSinifiMi(sinifAdi) {
  return /^fa-[a-z0-9][a-z0-9-]*$/.test(sinifAdi) &&
    !stilSiniflari.has(sinifAdi) &&
    !yardimciSinifDesenleri.some((desen) => desen.test(sinifAdi));
}

function kullanilanIkonlariBul() {
  const ikonlar = new Set();
  const sinifDeseni = /\bfa(?:-[a-z0-9]+)+\b/g;

  for (const dosya of taramaKokleri.flatMap(dosyalariGez)) {
    const icerik = fs.readFileSync(dosya, "utf8");
    for (const eslesme of icerik.matchAll(sinifDeseni)) {
      const sinifAdi = eslesme[0];
      if (ikonSinifiMi(sinifAdi)) {
        ikonlar.add(sinifAdi);
      }
    }
  }

  return ikonlar;
}

function ikonKurallariniSec(allCss, ikonlar) {
  const kurallar = [];
  const kuralDeseni = /([^{}]+)\{--fa:"(?:\\.|[^"])*"\}/g;

  for (const eslesme of allCss.matchAll(kuralDeseni)) {
    const kural = eslesme[0];
    const secici = eslesme[1];
    const siniflar = Array.from(secici.matchAll(/\.([a-z0-9-]+)/g), (sinif) => sinif[1]);

    if (siniflar.some((sinif) => ikonlar.has(sinif))) {
      kurallar.push(kural);
    }
  }

  return kurallar;
}

const allCss = fs.readFileSync(kaynakCssYolu, "utf8");
const ikonlar = kullanilanIkonlariBul();
const ikonKurallari = ikonKurallariniSec(allCss, ikonlar);
const eksikIkonlar = Array.from(ikonlar).filter((ikon) => !ikonKurallari.some((kural) => kural.includes(`.${ikon}`)));

if (eksikIkonlar.length > 0) {
  throw new Error(`Font Awesome kurali bulunamayan ikonlar: ${eksikIkonlar.sort().join(", ")}`);
}

const temelCss = [
  "/*! Font Awesome Free 7.2.0 subset for Misharix. Source: css/all.min.css */",
  ':host,:root{--fa-family-classic:"Font Awesome 7 Free";--fa-family-brands:"Font Awesome 7 Brands";--fa-font-solid:normal 900 1em/1 var(--fa-family-classic);--fa-font-regular:normal 400 1em/1 var(--fa-family-classic);--fa-font-brands:normal 400 1em/1 var(--fa-family-brands);--fa-style-family-classic:var(--fa-family-classic)}',
  '@font-face{font-family:"Font Awesome 7 Free";font-style:normal;font-weight:900;font-display:swap;src:url(../webfonts/fa-solid-900.woff2)}',
  '@font-face{font-family:"Font Awesome 7 Free";font-style:normal;font-weight:400;font-display:swap;src:url(../webfonts/fa-regular-400.woff2)}',
  '@font-face{font-family:"Font Awesome 7 Brands";font-style:normal;font-weight:400;font-display:swap;src:url(../webfonts/fa-brands-400.woff2)}',
  '.fa,.fa-brands,.fa-classic,.fa-regular,.fa-solid,.fab,.far,.fas{--_fa-family:var(--fa-family,var(--fa-style-family,"Font Awesome 7 Free"));-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale;display:var(--fa-display,inline-block);font-family:var(--_fa-family);font-feature-settings:normal;font-style:normal;font-synthesis:none;font-variant:normal;font-weight:var(--fa-style,900);line-height:1;text-align:center;text-rendering:auto;width:var(--fa-width,1.25em)}',
  ':is(.fas,.far,.fab,.fa-solid,.fa-regular,.fa-brands,.fa-classic,.fa):before{content:var(--fa)/""}',
  '@supports not (content:""/""){:is(.fas,.far,.fab,.fa-solid,.fa-regular,.fa-brands,.fa-classic,.fa):before{content:var(--fa)}}',
  '.fa-solid,.fas{--fa-family:var(--fa-family-classic);--fa-style:900}',
  '.fa-regular,.far{--fa-family:var(--fa-family-classic);--fa-style:400}',
  '.fa-brands,.fab{--fa-family:var(--fa-family-brands);--fa-style:400}'
];

fs.writeFileSync(hedefCssYolu, `${temelCss.concat(ikonKurallari).join("")}\n`);

console.log(`ikonall.min.css guncellendi: ${ikonlar.size} ikon, ${ikonKurallari.length} kural`);
