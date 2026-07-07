#!/usr/bin/env node
// Misharix ↔ ECSPros yan yana ekran görüntüsü aracı (plan A8).
//
// Kullanım:
//   CHROME_PATH=/path/to/chrome node tools/misharix-sync/screenshot.mjs <url> <cikti-adi>
//   node tools/misharix-sync/screenshot.mjs http://localhost:5051/ anasayfa
//
// Desktop (1440px) + mobil (390px) iki görüntü alır:
//   tools/misharix-sync/shots/<cikti-adi>-desktop.png / -mobil.png
// Chromium kurulumu (root'suz): bkz. hafıza `reference_headless_chromium_no_root.md`
// (apt-get download + dpkg-deb -x + LD_LIBRARY_PATH; playwright-core npm'den).

import { chromium } from "playwright-core";
import { mkdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const [url, ad] = process.argv.slice(2);
if (!url || !ad) {
  console.error("Kullanım: node screenshot.mjs <url> <cikti-adi>");
  process.exit(1);
}

const executablePath = process.env.CHROME_PATH;
if (!executablePath) {
  console.error("CHROME_PATH ortam değişkeni gerekli (root'suz chromium tarifi: reference_headless_chromium_no_root.md)");
  process.exit(1);
}

const buraya = path.dirname(fileURLToPath(import.meta.url));
const ciktiDizini = path.join(buraya, "shots");
mkdirSync(ciktiDizini, { recursive: true });

const tarayici = await chromium.launch({ executablePath, args: ["--no-sandbox"] });

for (const [etiket, viewport] of [
  ["desktop", { width: 1440, height: 900 }],
  ["mobil", { width: 390, height: 844 }],
]) {
  const sayfa = await tarayici.newPage({ viewport });
  const konsolHatalari = [];
  sayfa.on("console", (m) => m.type() === "error" && konsolHatalari.push(m.text()));
  sayfa.on("pageerror", (e) => konsolHatalari.push(String(e)));

  await sayfa.goto(url, { waitUntil: "networkidle", timeout: 30000 });
  await sayfa.waitForTimeout(500);

  const dosya = path.join(ciktiDizini, `${ad}-${etiket}.png`);
  await sayfa.screenshot({ path: dosya, fullPage: true });
  console.log(`✓ ${dosya}`);
  if (konsolHatalari.length) {
    console.log(`  ⚠ ${etiket} konsol hataları (${konsolHatalari.length}):`);
    konsolHatalari.slice(0, 5).forEach((h) => console.log(`    - ${h}`));
  }
  await sayfa.close();
}

await tarayici.close();
