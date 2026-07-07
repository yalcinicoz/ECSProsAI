// ECSPros mağaza — global UI davranışları.
// Misharix tasarım sisteminden portlandı (bkz. /opt/misharixWebSites/misharix/wwwroot/js/site.js).
// Bu dosyada veri üretimi veya sayfaya özel fetch/listeleme mantığı tutulmaz — o app.js'de kalır.

// ─────────────────────────────────────────────────────────
// Sayfa modülü registry davranışı.
// ─────────────────────────────────────────────────────────
(() => {
  const msPageModulDurum = new WeakMap();
  const msPageModuller = window.msPageModules || {};

  window.msPageModules = msPageModuller;

  window.msRegisterPageModule = (modulAdi, baslatici) => {
    if (!modulAdi || typeof baslatici !== "function") return;
    msPageModuller[String(modulAdi).trim()] = baslatici;
  };

  window.msRunPageModules = (kok = document) => {
    if (!kok?.querySelectorAll) return;

    const alanlar = [kok, ...Array.from(kok.querySelectorAll("[data-ms-page-module],[data-ms-page-script]"))];
    const tekilAlanlar = Array.from(new Set(alanlar));

    tekilAlanlar.forEach((modulAlani) => {
      if (!modulAlani?.dataset) return;

      const modulAdlari = `${modulAlani.dataset.msPageModule || modulAlani.dataset.msPageScript || ""}`
        .split(",").map((ad) => ad.trim()).filter(Boolean);
      if (!modulAdlari.length) return;

      let calisanModuller = msPageModulDurum.get(modulAlani);
      if (!calisanModuller) {
        calisanModuller = new Set();
        msPageModulDurum.set(modulAlani, calisanModuller);
      }

      modulAdlari.forEach((modulAdi) => {
        if (calisanModuller.has(modulAdi)) return;
        const baslatici = msPageModuller[modulAdi];
        if (typeof baslatici !== "function") return;

        try {
          baslatici(modulAlani);
          calisanModuller.add(modulAdi);
        } catch (error) {
          console.error(`ms page module failed: ${modulAdi}`, error);
        }
      });
    });
  };
})();

// ─────────────────────────────────────────────────────────
// Global image lazy load davranışı (.lazy-infinite-on scope'unda opt-in).
// ─────────────────────────────────────────────────────────
(() => {
  const placeholderSrc = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='32' height='42' viewBox='0 0 32 42'%3E%3Crect width='32' height='42' fill='%23f1f5f9'/%3E%3C/svg%3E";
  const lazyInfiniteSecici = ".lazy-infinite-on";
  let lazyObserver = null;

  const lazyInfiniteAktifMi = (oge) => Boolean(oge?.closest?.(lazyInfiniteSecici));

  const lazyKapsamlariBul = (kok = document) => {
    if (kok instanceof HTMLImageElement) return lazyInfiniteAktifMi(kok) ? [kok] : [];
    if (kok instanceof Element && lazyInfiniteAktifMi(kok)) return [kok];
    if (!kok?.querySelectorAll) return [];
    return Array.from(kok.querySelectorAll(lazyInfiniteSecici));
  };

  const skeletonHazirla = (img) => {
    if (img.dataset.msLazySkeleton !== "true" || img.closest(".ms-lazy-placeholderli")) return;
    if (!img.parentNode) return;

    const kapsayici = document.createElement("span");
    kapsayici.className = "ms-lazy-placeholderli";
    img.parentNode.insertBefore(kapsayici, img);
    kapsayici.appendChild(img);

    const skeleton = document.createElement("span");
    skeleton.className = "ms-lazy-skeleton";
    skeleton.setAttribute("aria-hidden", "true");
    kapsayici.appendChild(skeleton);
  };

  const gorselYukle = (img) => {
    const lazySrc = img.dataset.msLazySrc;
    const lazySrcset = img.dataset.msLazySrcset;
    if (!lazySrc && !lazySrcset) {
      img.classList.add("ms-lazy-gorsel-yuklendi");
      return;
    }

    img.addEventListener("load", () => img.classList.add("ms-lazy-gorsel-yuklendi"), { once: true });
    if (img.dataset.msLazySizes) img.sizes = img.dataset.msLazySizes;
    if (lazySrcset) img.srcset = lazySrcset;
    if (lazySrc) img.src = lazySrc;

    img.removeAttribute("data-ms-lazy-src");
    img.removeAttribute("data-ms-lazy-srcset");
    img.removeAttribute("data-ms-lazy-sizes");
  };

  const gorselHazirla = (img) => {
    if (!(img instanceof HTMLImageElement) || !lazyInfiniteAktifMi(img) || img.dataset.msLazyHazir === "true"
      || img.dataset.msLazy === "false" || img.classList.contains("no-lazy")) return;

    img.dataset.msLazyHazir = "true";
    if (!img.hasAttribute("loading")) img.loading = "lazy";
    if (!img.hasAttribute("decoding")) img.decoding = "async";
    if (!img.dataset.msLazySrc && !img.dataset.msLazySrcset) return;

    img.classList.add("ms-lazy-gorsel");
    skeletonHazirla(img);
    if (!img.getAttribute("src")) img.src = placeholderSrc;

    if (lazyObserver) lazyObserver.observe(img);
    else gorselYukle(img);
  };

  const lazyLoadYenile = (kok = document) => {
    lazyKapsamlariBul(kok).forEach((kapsam) => {
      if (kapsam instanceof HTMLImageElement) { gorselHazirla(kapsam); return; }
      kapsam.querySelectorAll("img").forEach(gorselHazirla);
    });
  };

  if ("IntersectionObserver" in window) {
    lazyObserver = new IntersectionObserver((entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        observer.unobserve(entry.target);
        gorselYukle(entry.target);
      });
    }, { rootMargin: "240px 0px", threshold: 0.01 });
  }

  window.msLazyLoadYenile = lazyLoadYenile;
  lazyLoadYenile();
})();

// ─────────────────────────────────────────────────────────
// Opt-in infinite scroll — gerçek API sayfalamasıyla çalışır.
// Misharix'in template-klonlama demo motorundan FARKLI: burada veri her zaman
// backend'den gelir. Sayfa, kendi yükleyicisini window.msInfiniteLoaders[ad]'a
// kaydeder (async fn, daha fazla sayfa yoksa false döner); bu modül sadece
// scroll-eşiği tetiklendiğinde o yükleyiciyi çağırır.
// ─────────────────────────────────────────────────────────
(() => {
  const hazirAlanlar = new WeakSet();
  window.msInfiniteLoaders = window.msInfiniteLoaders || {};

  const alanBaslat = (alan) => {
    if (!alan || hazirAlanlar.has(alan)) return;

    const yukleyiciAdi = alan.dataset.msInfiniteYukleyici || "";
    const yukleyici = yukleyiciAdi ? window.msInfiniteLoaders[yukleyiciAdi] : null;
    if (!yukleyici) return;

    hazirAlanlar.add(alan);
    const esik = Number.parseFloat(alan.dataset.msInfiniteEsik || "0.8");
    const yukleniyor = alan.querySelector("[data-ms-infinite-yukleniyor]");
    let yuklemeVar = false;
    let bitti = false;

    const kontrolEt = async () => {
      if (yuklemeVar || bitti) return;

      const liste = alan.querySelector("[data-ms-infinite-liste]") || alan;
      const rect = liste.getBoundingClientRect();
      const listeBaslangic = rect.top + window.scrollY;
      const ilerleme = (window.scrollY + window.innerHeight - listeBaslangic) / Math.max(liste.offsetHeight, 1);
      if (ilerleme < esik) return;

      yuklemeVar = true;
      yukleniyor?.classList.add("ms-aktif");

      try {
        const devamEdecekMi = await yukleyici(alan);
        if (devamEdecekMi === false) bitti = true;
      } catch (error) {
        console.error("infinite-scroll yükleyici hatası", error);
      } finally {
        yuklemeVar = false;
        yukleniyor?.classList.remove("ms-aktif");
      }
    };

    window.addEventListener("scroll", kontrolEt, { passive: true });
    window.addEventListener("resize", kontrolEt);
    window.setTimeout(kontrolEt, 140);
  };

  const baslat = (kok = document) => {
    const adaylar = [];
    if (kok?.matches?.("[data-ms-infinite-scroll]")) adaylar.push(kok);
    kok?.querySelectorAll?.("[data-ms-infinite-scroll]").forEach((a) => adaylar.push(a));
    adaylar.forEach(alanBaslat);
  };

  window.msInfiniteScrollBaslat = baslat;
  window.msRegisterPageModule("infinite-scroll", baslat);
})();

// ─────────────────────────────────────────────────────────
// Genel modal aç/kapa (ms-ornek-modal iskeleti — tüm modaller bunu kullanır).
// ─────────────────────────────────────────────────────────
(() => {
  const boyutClasslari = ["ms-ornek-modal-boyut-m", "ms-ornek-modal-boyut-l", "ms-ornek-modal-boyut-xl", "ms-ornek-modal-boyut-2xl"];
  let sonOdaklananEleman = null;

  const alanlariSec = (kok, secici) => {
    if (!kok?.querySelectorAll) return [];
    const alanlar = [];
    if (kok.matches?.(secici)) alanlar.push(kok);
    kok.querySelectorAll(secici).forEach((alan) => alanlar.push(alan));
    return Array.from(new Set(alanlar));
  };

  const modalKapat = () => {
    document.querySelectorAll("[data-ms-ornek-modal]").forEach((modal) => {
      modal.classList.remove("ms-ornek-modal-acik");
      modal.setAttribute("aria-hidden", "true");
    });
    document.body.style.overflow = "";
    sonOdaklananEleman?.focus?.();
  };

  const modalAc = (modalTuru, modalBoyutu = "m") => {
    const modal = document.querySelector(`[data-ms-ornek-modal="${modalTuru}"]`);
    if (!modal) return;

    const modalKutusu = modal.querySelector(".ms-ornek-modal-kutu");
    modalKutusu?.classList.remove(...boyutClasslari);
    modalKutusu?.classList.add(`ms-ornek-modal-boyut-${modalBoyutu}`);

    sonOdaklananEleman = document.activeElement;
    modalKapat();
    modal.classList.add("ms-ornek-modal-acik");
    modal.setAttribute("aria-hidden", "false");
    document.body.style.overflow = "hidden";
    window.setTimeout(() => modal.querySelector("button, a")?.focus(), 40);
  };

  const baslat = (kok = document) => {
    alanlariSec(kok, "[data-ms-ornek-modal-ac]").forEach((buton) => {
      if (buton.dataset.msOrnekModalHazir === "true") return;
      buton.dataset.msOrnekModalHazir = "true";
      buton.addEventListener("click", () => modalAc(buton.dataset.msOrnekModalAc, buton.dataset.msOrnekModalBoyut || "m"));
    });

    alanlariSec(kok, "[data-ms-ornek-modal]").forEach((modal) => {
      modal.querySelectorAll("[data-ms-ornek-modal-kapat]").forEach((kapatici) => {
        if (kapatici.dataset.msOrnekModalKapatHazir === "true") return;
        kapatici.dataset.msOrnekModalKapatHazir = "true";
        kapatici.addEventListener("click", modalKapat);
      });
    });
  };

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") modalKapat();
  });

  window.msOrnekModalAc = modalAc;
  window.msOrnekModalKapat = modalKapat;
  window.msRegisterPageModule("ornek-modal", baslat);
})();

// ─────────────────────────────────────────────────────────
// Filtre akordiyon blokları (sidebar filtre grupları).
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-filter-block]").forEach((filtre) => {
      if (filtre.dataset.msFiltreHazir === "true") return;
      filtre.dataset.msFiltreHazir = "true";

      const buton = filtre.querySelector("[data-filter-toggle]");
      const icerik = filtre.querySelector("[data-filter-content]");
      const ok = filtre.querySelector(".ms-filtre-ok");
      const arama = filtre.querySelector("[data-filter-search]");
      const secimler = filtre.querySelectorAll("[data-filter-option]");
      if (!buton || !icerik) return;

      buton.addEventListener("click", () => {
        const acik = buton.getAttribute("aria-expanded") === "true";
        buton.setAttribute("aria-expanded", (!acik).toString());
        icerik.classList.toggle("ms-gizli", acik);
        ok?.classList.toggle("ms-filtre-ok-acik", !acik);
      });

      if (arama) {
        arama.addEventListener("input", () => {
          const aranan = arama.value.trim().toLocaleLowerCase("tr-TR");
          secimler.forEach((secim) => {
            secim.hidden = !secim.textContent.trim().toLocaleLowerCase("tr-TR").includes(aranan);
          });
        });
      }
    });
  };

  window.msFiltreBloklariBaslat = baslat;
  window.msRegisterPageModule("filtre-bloklari", baslat);
})();

// ─────────────────────────────────────────────────────────
// Sıralama select (dropdown, ms-siralama-select).
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-siralama-select]").forEach((select) => {
      if (select.dataset.msSiralamaHazir === "true") return;
      select.dataset.msSiralamaHazir = "true";

      const tetikleyici = select.querySelector("[data-ms-siralama-tetikleyici]");
      const deger = select.querySelector("[data-ms-siralama-deger]");
      const secenekler = select.querySelectorAll("[data-ms-siralama-secenek]");
      if (!tetikleyici || !deger) return;

      const kapat = () => {
        select.classList.remove("ms-siralama-select-acik");
        tetikleyici.setAttribute("aria-expanded", "false");
      };

      tetikleyici.addEventListener("click", () => {
        const acik = select.classList.toggle("ms-siralama-select-acik");
        tetikleyici.setAttribute("aria-expanded", acik.toString());
      });

      secenekler.forEach((secenek) => {
        secenek.addEventListener("click", () => {
          deger.textContent = secenek.textContent.trim();
          secenekler.forEach((oge) => {
            const aktif = oge === secenek;
            oge.classList.toggle("ms-siralama-select-secenek-aktif", aktif);
            oge.setAttribute("aria-selected", aktif.toString());
          });
          select.dispatchEvent(new CustomEvent("ms-siralama-degisti", { detail: { value: secenek.dataset.msSiralamaSecenek || secenek.textContent.trim() } }));
          kapat();
        });
      });

      document.addEventListener("click", (event) => {
        if (!select.contains(event.target)) kapat();
      });
    });
  };

  window.msSiralamaSelectleriBaslat = baslat;
  window.msRegisterPageModule("siralama-select", baslat);
})();

// ─────────────────────────────────────────────────────────
// Özel select (data-ms-ozel-select — tekli/çoklu/checkboxlı, arama destekli).
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-ozel-select]").forEach((select) => {
      if (select.dataset.msOzelSelectHazir === "true") return;
      select.dataset.msOzelSelectHazir = "true";

      const tetikleyici = select.querySelector("[data-ms-ozel-select-tetikleyici]");
      const deger = select.querySelector("[data-ms-ozel-select-deger]");
      const secenekler = select.querySelectorAll("[data-ms-ozel-select-secenek]");
      const arama = select.querySelector("[data-ms-ozel-select-arama]");
      const coklu = select.hasAttribute("data-ms-ozel-select-coklu");
      const checkboxli = select.hasAttribute("data-ms-ozel-select-checkboxli");
      const temizleButonu = select.querySelector("[data-ms-ozel-select-temizle]");
      const uygulaButonu = select.querySelector("[data-ms-ozel-select-uygula]");
      if (!tetikleyici || !deger) return;

      const secenekMetniAl = (secenek) => secenek.querySelector("[data-ms-ozel-select-metin]")?.textContent.trim()
        || secenek.querySelector("span:last-child")?.textContent.trim() || secenek.textContent.trim();

      const kapat = () => {
        select.classList.remove("ms-ozel-select-acik");
        tetikleyici.setAttribute("aria-expanded", "false");
      };

      tetikleyici.addEventListener("click", () => {
        const acik = select.classList.toggle("ms-ozel-select-acik");
        tetikleyici.setAttribute("aria-expanded", acik.toString());
        if (acik && arama) window.setTimeout(() => arama.focus(), 30);
      });

      secenekler.forEach((secenek) => {
        secenek.addEventListener("click", (event) => {
          if (checkboxli) {
            const checkbox = secenek.querySelector("input[type='checkbox']");
            if (checkbox && event.target !== checkbox) {
              event.preventDefault();
              checkbox.checked = !checkbox.checked;
            }
            return;
          }

          if (coklu) {
            secenek.classList.toggle("ms-ozel-select-secenek-aktif");
            return;
          }

          deger.textContent = secenekMetniAl(secenek);
          secenekler.forEach((oge) => oge.classList.toggle("ms-ozel-select-secenek-aktif", oge === secenek));
          select.dispatchEvent(new CustomEvent("ms-ozel-select-degisti", { detail: { value: secenek.dataset.msOzelSelectDeger || secenekMetniAl(secenek) } }));
          kapat();
        });
      });

      if (arama) {
        arama.addEventListener("input", () => {
          const aranan = arama.value.trim().toLocaleLowerCase("tr-TR");
          secenekler.forEach((secenek) => {
            secenek.hidden = !secenek.textContent.trim().toLocaleLowerCase("tr-TR").includes(aranan);
          });
        });
      }

      temizleButonu?.addEventListener("click", () => {
        secenekler.forEach((secenek) => secenek.classList.remove("ms-ozel-select-secenek-aktif"));
      });
      uygulaButonu?.addEventListener("click", kapat);

      document.addEventListener("click", (event) => {
        if (!select.contains(event.target)) kapat();
      });
    });
  };

  window.msOzelSelectleriBaslat = baslat;
  window.msRegisterPageModule("ozel-select", baslat);
})();

// ─────────────────────────────────────────────────────────
// Carousel / yatay kaydırma (ms-gorunum-carousel — ana sayfa vitrinleri, kampanya şeridi).
// ─────────────────────────────────────────────────────────
(() => {
  const alanlariSec = (kok, secici) => {
    if (!kok?.querySelectorAll) return [];
    const alanlar = [];
    if (kok.matches?.(secici)) alanlar.push(kok);
    kok.querySelectorAll(secici).forEach((alan) => alanlar.push(alan));
    return Array.from(new Set(alanlar));
  };

  const baslat = (kok = document) => {
    alanlariSec(kok, "[data-ms-gorunum-carousel]").forEach((carousel) => {
      if (carousel.dataset.msGorunumCarouselHazir === "true") return;
      carousel.dataset.msGorunumCarouselHazir = "true";

      const liste = carousel.querySelector("[data-ms-gorunum-carousel-liste]");
      const solKontrol = carousel.querySelector("[data-ms-gorunum-carousel-kontrol='sol']");
      const sagKontrol = carousel.querySelector("[data-ms-gorunum-carousel-kontrol='sag']");
      const sayac = carousel.querySelector("[data-ms-gorunum-carousel-sayac]");
      let surukleniyor = false, suruklemeYapildi = false, baslangicX = 0, baslangicScroll = 0, tiklamaEngellenecek = false;
      if (!liste) return;

      const kartlariAl = () => Array.from(liste.children);
      const enYuksekScrolluAl = () => Math.max(0, liste.scrollWidth - liste.clientWidth);
      const scrolluSinirla = (deger) => Math.min(enYuksekScrolluAl(), Math.max(0, deger));
      const kartScrollSolunuAl = (kart) => scrolluSinirla(kart.offsetLeft - liste.offsetLeft);

      const aktifKartIndexiniBul = () => {
        const kartlar = kartlariAl();
        if (!kartlar.length) return 0;
        let aktifIndex = 0, enYakinMesafe = Number.POSITIVE_INFINITY;
        kartlar.forEach((kart, index) => {
          const mesafe = Math.abs(kartScrollSolunuAl(kart) - liste.scrollLeft);
          if (mesafe < enYakinMesafe) { aktifIndex = index; enYakinMesafe = mesafe; }
        });
        return aktifIndex;
      };

      const kartaGit = (index, behavior = "smooth") => {
        const kartlar = kartlariAl();
        if (!kartlar.length) return;
        const hedefIndex = Math.min(kartlar.length - 1, Math.max(0, index));
        liste.scrollTo({ left: kartScrollSolunuAl(kartlar[hedefIndex]), behavior });
      };

      const guncelle = () => {
        const kartlar = kartlariAl();
        const kaydirilabilir = liste.scrollWidth > liste.clientWidth + 2;
        const basta = liste.scrollLeft <= 1;
        const sonda = liste.scrollLeft + liste.clientWidth >= liste.scrollWidth - 1;
        solKontrol?.toggleAttribute("disabled", !kaydirilabilir || basta);
        sagKontrol?.toggleAttribute("disabled", !kaydirilabilir || sonda);
        if (sayac && kartlar.length > 0) sayac.textContent = `${aktifKartIndexiniBul() + 1} / ${kartlar.length}`;
      };

      carousel.msGorunumCarouselGuncelle = () => window.requestAnimationFrame(guncelle);

      solKontrol?.addEventListener("click", () => kartaGit(aktifKartIndexiniBul() - 1));
      sagKontrol?.addEventListener("click", () => kartaGit(aktifKartIndexiniBul() + 1));
      liste.addEventListener("scroll", guncelle, { passive: true });
      liste.addEventListener("dragstart", (event) => event.preventDefault());
      liste.addEventListener("click", (event) => {
        if (tiklamaEngellenecek) { event.preventDefault(); tiklamaEngellenecek = false; }
      });
      liste.addEventListener("pointerdown", (event) => {
        if (event.button !== undefined && event.button !== 0) return;
        surukleniyor = true; suruklemeYapildi = false; tiklamaEngellenecek = false;
        baslangicX = event.clientX; baslangicScroll = liste.scrollLeft;
        liste.classList.add("ms-gorunum-carousel-surukleniyor");
        liste.setPointerCapture?.(event.pointerId);
      });
      liste.addEventListener("pointermove", (event) => {
        if (!surukleniyor) return;
        const fark = event.clientX - baslangicX;
        if (Math.abs(fark) > 6) { suruklemeYapildi = true; tiklamaEngellenecek = true; event.preventDefault(); }
        liste.scrollLeft = baslangicScroll - fark;
        guncelle();
      });

      const suruklemeyiBitir = (event) => {
        if (!surukleniyor) return;
        const hizalanacakIndex = suruklemeYapildi ? aktifKartIndexiniBul() : -1;
        surukleniyor = false;
        liste.classList.remove("ms-gorunum-carousel-surukleniyor");
        if (typeof event.pointerId === "number" && liste.hasPointerCapture?.(event.pointerId)) liste.releasePointerCapture(event.pointerId);
        if (hizalanacakIndex >= 0) kartaGit(hizalanacakIndex); else guncelle();
      };

      liste.addEventListener("pointerup", suruklemeyiBitir);
      liste.addEventListener("pointercancel", suruklemeyiBitir);
      liste.addEventListener("mouseleave", suruklemeyiBitir);
      window.addEventListener("resize", guncelle);
      window.requestAnimationFrame(guncelle);
    });

    // İçerik sekmeleri (ör. ana sayfa "Çok Satanlar / Yeni Gelenler / İndirimde").
    alanlariSec(kok, "[data-ms-gorunum-icerik-tabs]").forEach((tabAlani) => {
      if (tabAlani.dataset.msGorunumIcerikTabsHazir === "true") return;
      tabAlani.dataset.msGorunumIcerikTabsHazir = "true";

      const sekmeler = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-icerik-tab]"));
      const paneller = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-icerik-panel]"));

      const panelGoster = (hedef) => {
        sekmeler.forEach((sekme) => {
          const aktif = sekme.dataset.msGorunumIcerikTab === hedef;
          sekme.classList.toggle("ms-gorunum-mini-tab-aktif", aktif);
          sekme.setAttribute("aria-selected", aktif.toString());
        });
        paneller.forEach((panel) => {
          panel.hidden = panel.dataset.msGorunumIcerikPanel !== hedef;
        });
      };

      sekmeler.forEach((sekme) => {
        sekme.addEventListener("click", () => panelGoster(sekme.dataset.msGorunumIcerikTab));
      });

      if (sekmeler[0]) panelGoster(sekmeler[0].dataset.msGorunumIcerikTab);
    });
  };

  window.msGorunumCarouselBaslat = baslat;
  window.msRegisterPageModule("gorunum-carousel", baslat);
})();

// ─────────────────────────────────────────────────────────
// Ürün kartı davranışları: tam-kart tıklama, mini galeri hover/touch,
// favori kalp animasyonu (yerel/localStorage — henüz backend'i yok), renk tooltip.
// ─────────────────────────────────────────────────────────
(() => {
  const kalpSvg = (dolu) => `<svg viewBox="0 0 24 24" width="16" height="16" fill="${dolu ? "currentColor" : "none"}" stroke="currentColor" stroke-width="1.8"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>`;

  const kartTiklamasiniHazirla = (kart) => {
    if (!kart || kart.dataset.msKartLinkHazir === "true") return;
    kart.dataset.msKartLinkHazir = "true";
    kart.addEventListener("click", (event) => {
      if (event.target.closest("a, button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], .ms-urun-favori, .ms-urun-renk-rozet, .ms-urun-renk-tooltip-alani")) return;
      kart.querySelector("[data-ms-kart-link]")?.click();
    });
  };

  const galeriHazirla = (kok) => {
    const etkilesimliMi = (hedef) => hedef instanceof Element
      && Boolean(hedef.closest("button, a, input, label, [role='button'], .ms-urun-favori, .ms-urun-renk-rozet"));

    kok.querySelectorAll("[data-ms-urun-galeri]").forEach((galeri) => {
      if (galeri.dataset.msUrunGaleriHazir === "true") return;
      galeri.dataset.msUrunGaleriHazir = "true";

      const gorsel = galeri.querySelector("[data-ms-urun-galeri-gorsel]");
      const resimler = (galeri.dataset.msUrunGaleriResimler || "").split("|").filter(Boolean);
      const noktalar = galeri.querySelectorAll(".ms-urun-slider-noktalari span");
      let aktifIndex = 0;
      let dokunmaBaslangicX = 0, dokunmaBaslangicY = 0, dokunmaIslendi = false;
      if (!gorsel || resimler.length < 2) return;

      const gorselDegistir = (index) => {
        const hedefIndex = Math.max(0, Math.min(index, resimler.length - 1));
        if (hedefIndex === aktifIndex && gorsel.src === resimler[hedefIndex]) return;
        aktifIndex = hedefIndex;
        gorsel.src = resimler[hedefIndex];
        noktalar.forEach((nokta, i) => nokta.classList.toggle("ms-urun-slider-nokta-aktif", i === hedefIndex));
      };

      galeri.addEventListener("mousemove", (event) => {
        if (etkilesimliMi(event.target)) return;
        const alan = galeri.getBoundingClientRect();
        const oran = (event.clientX - alan.left) / alan.width;
        gorselDegistir(Math.min(resimler.length - 1, Math.max(0, Math.floor(oran * resimler.length))));
      });
      galeri.addEventListener("mouseleave", () => gorselDegistir(0));

      galeri.addEventListener("touchstart", (event) => {
        if (etkilesimliMi(event.target)) { dokunmaIslendi = true; return; }
        const dokunma = event.touches[0];
        if (!dokunma) return;
        dokunmaBaslangicX = dokunma.clientX; dokunmaBaslangicY = dokunma.clientY; dokunmaIslendi = false;
      }, { passive: true });

      galeri.addEventListener("touchmove", (event) => {
        const dokunma = event.touches[0];
        if (!dokunma || dokunmaIslendi) return;
        const farkX = dokunma.clientX - dokunmaBaslangicX, farkY = dokunma.clientY - dokunmaBaslangicY;
        if (Math.abs(farkX) < 28 || Math.abs(farkX) < Math.abs(farkY)) return;
        gorselDegistir(aktifIndex + (farkX < 0 ? 1 : -1));
        dokunmaIslendi = true;
      }, { passive: true });

      galeri.addEventListener("touchend", () => { dokunmaIslendi = false; });
    });
  };

  const FAVORI_ANAHTAR = "ecspros_favoriler";
  const favoriListesiOku = () => {
    try { return new Set(JSON.parse(localStorage.getItem(FAVORI_ANAHTAR) || "[]")); }
    catch { return new Set(); }
  };
  const favoriListesiYaz = (set) => localStorage.setItem(FAVORI_ANAHTAR, JSON.stringify([...set]));

  const favoriHazirla = (kok) => {
    kok.querySelectorAll(".ms-urun-karti .ms-urun-favori, .ms-urun-detay-bilgi .ms-urun-favori").forEach((buton) => {
      if (buton.dataset.msUrunFavoriHazir === "true") return;
      buton.dataset.msUrunFavoriHazir = "true";

      const urunKodu = buton.dataset.msUrunFavoriKod || buton.closest("[data-urun-kodu]")?.dataset.urunKodu;
      const favoriler = favoriListesiOku();
      const ikonAlani = buton.querySelector(".ms-urun-favori-ikon");
      const gorselAlani = buton.closest(".ms-urun-gorsel-alani");

      const ikonuGuncelle = (aktif) => { if (ikonAlani) ikonAlani.innerHTML = kalpSvg(aktif); };
      const baslangicAktif = Boolean(urunKodu && favoriler.has(urunKodu));
      buton.classList.toggle("ms-urun-favori-aktif", baslangicAktif);
      ikonuGuncelle(baslangicAktif);

      buton.addEventListener("pointerdown", (event) => event.stopPropagation());
      buton.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();

        const aktif = buton.classList.toggle("ms-urun-favori-aktif");
        buton.setAttribute("aria-pressed", aktif.toString());
        buton.setAttribute("aria-label", aktif ? "Favorilerden çıkar" : "Favorilere ekle");
        ikonuGuncelle(aktif);

        if (urunKodu) {
          const guncel = favoriListesiOku();
          aktif ? guncel.add(urunKodu) : guncel.delete(urunKodu);
          favoriListesiYaz(guncel);
        }

        if (gorselAlani) {
          const merkezKalp = document.createElement("span");
          merkezKalp.className = aktif ? "ms-urun-favori-merkez-kalp" : "ms-urun-favori-kirik-kalp";
          merkezKalp.innerHTML = kalpSvg(aktif);
          gorselAlani.appendChild(merkezKalp);
          merkezKalp.addEventListener("animationend", () => merkezKalp.remove(), { once: true });
        }
      });
    });
  };

  const renkTooltipHazirla = (kok) => {
    const durumGuncelle = () => {
      const acikVar = Boolean(document.querySelector(".ms-urun-karti.ms-urun-renk-tooltip-acik"));
      document.body.classList.toggle("ms-urun-renk-tooltip-body-kilitli", acikVar);
    };
    const kapat = (kart) => { kart?.classList.remove("ms-urun-renk-tooltip-acik"); durumGuncelle(); };
    const ac = (kart) => {
      if (!kart) return;
      document.querySelectorAll(".ms-urun-karti.ms-urun-renk-tooltip-acik").forEach((k) => { if (k !== kart) k.classList.remove("ms-urun-renk-tooltip-acik"); });
      kart.classList.add("ms-urun-renk-tooltip-acik");
      durumGuncelle();
    };

    kok.querySelectorAll(".ms-urun-renk-rozet").forEach((rozet) => {
      if (rozet.dataset.msUrunRenkHazir === "true") return;
      rozet.dataset.msUrunRenkHazir = "true";

      const kart = rozet.closest(".ms-urun-karti");
      const tooltipAlani = kart?.querySelector(".ms-urun-renk-tooltip-alani");
      let kapatmaZamani;
      const mobilMi = () => window.matchMedia("(max-width: 639px)").matches;
      if (!kart || !tooltipAlani) return;

      rozet.addEventListener("mouseenter", () => { if (!mobilMi()) { window.clearTimeout(kapatmaZamani); ac(kart); } });
      rozet.addEventListener("mouseleave", () => {
        if (mobilMi()) return;
        kapatmaZamani = window.setTimeout(() => {
          if (!rozet.matches(":hover") && !tooltipAlani.matches(":hover")) kapat(kart);
        }, 120);
      });
      rozet.addEventListener("click", (event) => {
        event.preventDefault(); event.stopPropagation();
        kart.classList.contains("ms-urun-renk-tooltip-acik") ? kapat(kart) : ac(kart);
      });
      document.addEventListener("click", (event) => {
        if (kart.classList.contains("ms-urun-renk-tooltip-acik") && !rozet.contains(event.target) && !tooltipAlani.contains(event.target)) {
          kapat(kart);
        }
      }, true);
    });
  };

  window.msUrunKartDavranislariYenile = (kok = document) => {
    if (!kok.querySelectorAll) return;
    kok.querySelectorAll("[data-ms-kart-link-alani]").forEach(kartTiklamasiniHazirla);
    galeriHazirla(kok);
    favoriHazirla(kok);
    renkTooltipHazirla(kok);
  };

  window.msRegisterPageModule("urun-karti", (kok) => window.msUrunKartDavranislariYenile(kok));
})();

// ─────────────────────────────────────────────────────────
// Ürün detay galerisi: küçük resim rayı + sürükle-geçiş + lightbox.
// (Not: Misharix'in tam sürümündeki hover-zoom lensi ve modal pinch-zoom'u
// bu portta basitleştirildi — bkz. PROGRESS.md.)
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-urun-detay-resim-alani]").forEach((alan) => {
      if (alan.dataset.msUrunDetayResimHazir === "true") return;
      alan.dataset.msUrunDetayResimHazir = "true";

      const anaKapsayici = alan.querySelector("[data-ms-urun-detay-resim-surukle]");
      const track = alan.querySelector("[data-ms-urun-detay-resim-track]");
      const slaytlar = Array.from(alan.querySelectorAll("[data-ms-urun-detay-resim-slide]"));
      const thumbButonlari = Array.from(alan.querySelectorAll("[data-ms-urun-detay-resim-thumb]"));
      const yonButonlari = alan.querySelectorAll("[data-ms-urun-detay-resim-yon]");
      const modal = alan.querySelector("[data-ms-urun-detay-resim-modal]");
      const modalGorsel = alan.querySelector("[data-ms-urun-detay-resim-modal-gorsel]");
      const modalKapaticilar = alan.querySelectorAll("[data-ms-urun-detay-resim-modal-kapat]");
      let aktifIndex = 0, surukleniyor = false, baslangicX = 0, suruklemeFarki = 0, tiklamaEngellenecek = false, gecisYapiliyor = false, gecisZamani, oncekiBodyOverflow = "";
      if (!anaKapsayici || !track || slaytlar.length === 0) return;

      const sar = (index) => (index + slaytlar.length) % slaytlar.length;

      const thumbGuncelle = () => {
        thumbButonlari.forEach((buton, i) => {
          const aktif = i === aktifIndex;
          buton.classList.toggle("ms-urun-detay-resim-thumb-aktif", aktif);
          buton.setAttribute("aria-pressed", aktif.toString());
        });
        thumbButonlari[aktifIndex]?.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
      };

      const konumlariGuncelle = (suruklemeYuzdesi = 0, yon = 0) => {
        const oncekiIndex = sar(aktifIndex - 1), sonrakiIndex = sar(aktifIndex + 1);
        slaytlar.forEach((slayt, i) => {
          let pozisyon = 200, gorunur = i === aktifIndex;
          if (i === aktifIndex) pozisyon = suruklemeYuzdesi;
          else if (yon < 0 && i === sonrakiIndex) { pozisyon = 100 + suruklemeYuzdesi; gorunur = true; }
          else if (yon > 0 && i === oncekiIndex) { pozisyon = -100 + suruklemeYuzdesi; gorunur = true; }
          slayt.style.transform = `translate3d(${pozisyon}%, 0, 0)`;
          slayt.classList.toggle("ms-urun-detay-resim-ana-gorunur", gorunur);
          slayt.classList.toggle("ms-urun-detay-resim-ana-aktif", i === aktifIndex);
        });
      };

      const gecisUygula = (hedefIndex, yon, hedefSurukleme) => {
        gecisYapiliyor = true;
        window.clearTimeout(gecisZamani);
        track.classList.add("ms-urun-detay-resim-gecis-hazirlaniyor");
        konumlariGuncelle(0, yon);
        track.offsetHeight;
        track.classList.remove("ms-urun-detay-resim-gecis-hazirlaniyor");
        window.requestAnimationFrame(() => konumlariGuncelle(hedefSurukleme, yon));

        gecisZamani = window.setTimeout(() => {
          aktifIndex = hedefIndex;
          gecisYapiliyor = false;
          track.classList.add("ms-urun-detay-resim-gecis-hazirlaniyor");
          konumlariGuncelle(0, 0);
          thumbGuncelle();
          track.offsetHeight;
          track.classList.remove("ms-urun-detay-resim-gecis-hazirlaniyor");
        }, 300);
      };

      const goster = (index) => {
        const hedefIndex = sar(index);
        if (hedefIndex === aktifIndex || gecisYapiliyor) return;
        let fark = hedefIndex - aktifIndex;
        if (Math.abs(fark) > slaytlar.length / 2) fark += fark > 0 ? -slaytlar.length : slaytlar.length;
        const yon = fark > 0 ? -1 : 1;
        gecisUygula(hedefIndex, yon, fark > 0 ? -100 : 100);
      };

      const surukleyerekGoster = (fark) => {
        const yon = fark > 0 ? -1 : 1;
        gecisUygula(sar(aktifIndex + fark), yon, fark > 0 ? -100 : 100);
      };

      const modalAc = () => {
        if (!modal || !modalGorsel) return;
        modalGorsel.src = slaytlar[aktifIndex].getAttribute("src") || "";
        modalGorsel.alt = slaytlar[aktifIndex].getAttribute("alt") || "Ürün görseli";
        modal.classList.add("ms-ornek-modal-acik");
        modal.setAttribute("aria-hidden", "false");
        oncekiBodyOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";
      };
      const modalKapat = () => {
        if (!modal) return;
        modal.classList.remove("ms-ornek-modal-acik");
        modal.setAttribute("aria-hidden", "true");
        document.body.style.overflow = oncekiBodyOverflow;
      };

      thumbButonlari.forEach((buton, index) => buton.addEventListener("click", () => goster(index)));
      yonButonlari.forEach((buton) => buton.addEventListener("click", () => goster(aktifIndex + (buton.dataset.msUrunDetayResimYon === "sonraki" ? 1 : -1))));
      modalKapaticilar.forEach((kapatici) => kapatici.addEventListener("click", modalKapat));
      document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && modal?.classList.contains("ms-ornek-modal-acik")) modalKapat();
      });

      anaKapsayici.addEventListener("dragstart", (event) => event.preventDefault());
      anaKapsayici.addEventListener("pointerdown", (event) => {
        if (event.button !== 0 || event.target.closest("[data-ms-urun-detay-resim-yon]") || gecisYapiliyor) return;
        surukleniyor = true; tiklamaEngellenecek = false; baslangicX = event.clientX; suruklemeFarki = 0;
        anaKapsayici.classList.add("ms-urun-detay-resim-surukleniyor");
        anaKapsayici.setPointerCapture?.(event.pointerId);
      });
      anaKapsayici.addEventListener("pointermove", (event) => {
        if (!surukleniyor) return;
        suruklemeFarki = event.clientX - baslangicX;
        if (Math.abs(suruklemeFarki) > 6) {
          tiklamaEngellenecek = true;
          event.preventDefault();
          konumlariGuncelle((suruklemeFarki / anaKapsayici.clientWidth) * 100, suruklemeFarki < 0 ? -1 : 1);
        }
      });

      const suruklemeyiBitir = (event) => {
        if (!surukleniyor) return;
        const esik = Math.max(48, (anaKapsayici?.clientWidth || 0) * 0.1);
        surukleniyor = false;
        anaKapsayici.classList.remove("ms-urun-detay-resim-surukleniyor");
        if (suruklemeFarki <= -esik) surukleyerekGoster(1);
        else if (suruklemeFarki >= esik) surukleyerekGoster(-1);
        else {
          konumlariGuncelle(0, suruklemeFarki < 0 ? -1 : 1);
          window.setTimeout(() => {
            track.classList.add("ms-urun-detay-resim-gecis-hazirlaniyor");
            konumlariGuncelle(0, 0);
            track.offsetHeight;
            track.classList.remove("ms-urun-detay-resim-gecis-hazirlaniyor");
          }, 300);
        }
        if (anaKapsayici.hasPointerCapture?.(event.pointerId)) anaKapsayici.releasePointerCapture(event.pointerId);
        suruklemeFarki = 0;
      };

      anaKapsayici.addEventListener("pointerup", suruklemeyiBitir);
      anaKapsayici.addEventListener("pointercancel", suruklemeyiBitir);
      anaKapsayici.addEventListener("lostpointercapture", suruklemeyiBitir);
      anaKapsayici.addEventListener("click", (event) => {
        if (event.target.closest("[data-ms-urun-detay-resim-yon]")) return;
        if (tiklamaEngellenecek) { event.preventDefault(); event.stopPropagation(); tiklamaEngellenecek = false; return; }
        modalAc();
      });

      konumlariGuncelle(0, 0);
      thumbGuncelle();
    });
  };

  window.msUrunDetayResimBaslat = baslat;
  window.msRegisterPageModule("urun-detay-resim", baslat);
})();

// ─────────────────────────────────────────────────────────
// Sekme grubu — genel amaçlı (data-ms-tab-grubu / -tab / -panel).
// Hesabım, Kurumsal gibi sonraki fazlarda kullanılacak; tek generic
// implementasyon, Misharix'teki her sayfaya özel kopyaların yerini alır.
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-tab-grubu]").forEach((grup) => {
      if (grup.dataset.msTabGrubuHazir === "true") return;
      grup.dataset.msTabGrubuHazir = "true";

      const sekmeler = Array.from(grup.querySelectorAll("[data-ms-tab]"));
      const paneller = Array.from(grup.querySelectorAll("[data-ms-panel]"));

      const goster = (hedef) => {
        sekmeler.forEach((sekme) => {
          const aktif = sekme.dataset.msTab === hedef;
          sekme.classList.toggle("ms-tab-aktif", aktif);
          sekme.setAttribute("aria-selected", aktif.toString());
        });
        paneller.forEach((panel) => { panel.hidden = panel.dataset.msPanel !== hedef; });
      };

      sekmeler.forEach((sekme) => sekme.addEventListener("click", () => goster(sekme.dataset.msTab)));
      if (sekmeler[0]) goster(sekmeler[0].dataset.msTab);
    });
  };

  window.msTabGrubuBaslat = baslat;
  window.msRegisterPageModule("tab-grubu", baslat);
})();

// ─────────────────────────────────────────────────────────
// Ana navigasyon: mobil menü aç/kapa, arama paneli, sepet dropdown, mega menü.
// Misharix'ten portlandı (_AnaNavigasyon.cshtml inline script). Kampanya şeridi,
// görsel arama ve giriş/kayıt modalleri bu portta YOK — Faz 1 kapsamı dışında
// (kampanya/görsel-arama backend'i yok; giriş Faz 2'de gelecek).
// ─────────────────────────────────────────────────────────
(() => {
  const panel = document.querySelector("[data-ms-mobil-menu]");
  const acButonu = document.querySelector("[data-ms-mobil-menu-ac]");
  if (!panel || !acButonu) return;

  const kapatButonlari = panel.querySelectorAll("[data-ms-mobil-menu-kapat]");
  let sonOdaklananEleman = null;

  const panelAc = () => {
    sonOdaklananEleman = document.activeElement;
    panel.classList.add("ms-ana-navigasyon-mobil-panel-acik");
    panel.setAttribute("aria-hidden", "false");
    acButonu.setAttribute("aria-expanded", "true");
    document.body.style.overflow = "hidden";
    window.setTimeout(() => panel.querySelector("[data-ms-mobil-menu-kapat]")?.focus(), 40);
  };
  const panelKapat = () => {
    panel.classList.remove("ms-ana-navigasyon-mobil-panel-acik");
    panel.setAttribute("aria-hidden", "true");
    acButonu.setAttribute("aria-expanded", "false");
    document.body.style.overflow = "";
    sonOdaklananEleman?.focus?.();
  };

  acButonu.addEventListener("click", panelAc);
  kapatButonlari.forEach((buton) => buton.addEventListener("click", panelKapat));
  panel.addEventListener("click", (event) => { if (event.target === panel) panelKapat(); });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && panel.classList.contains("ms-ana-navigasyon-mobil-panel-acik")) panelKapat();
  });

  window.msMobilMenuKapat = panelKapat;

  // Ana sekme (kök kategori) / yan sekme (alt kategori) — data JS ile initNav()'da doldurulur.
  panel.addEventListener("click", (event) => {
    const anaSekme = event.target.closest("[data-ms-mobil-ana-sekme]");
    if (anaSekme) {
      panel.querySelectorAll("[data-ms-mobil-ana-sekme]").forEach((s) => {
        const aktif = s === anaSekme;
        s.classList.toggle("ms-ana-navigasyon-mobil-ana-sekme-aktif", aktif);
        s.setAttribute("aria-pressed", aktif.toString());
      });
      panel.querySelectorAll("[data-ms-mobil-yan-grup]").forEach((grup) => {
        grup.hidden = grup.dataset.msMobilYanGrup !== anaSekme.dataset.msMobilAnaSekme;
      });
    }
  });
})();

// ─────────────────────────────────────────────────────────
// Arama paneli (genişleyen arama kutusu, gerçek canlı sonuçlarla).
// ─────────────────────────────────────────────────────────
(() => {
  document.querySelectorAll("[data-ms-arama]").forEach((aramaAlani) => {
    const input = aramaAlani.querySelector("[data-ms-arama-input]");
    const panel = aramaAlani.querySelector("[data-ms-arama-panel]");
    const kapat = aramaAlani.querySelector("[data-ms-arama-kapat]");
    const panelInput = aramaAlani.querySelector("[data-ms-arama-panel-input]");
    const temizleButonlari = aramaAlani.querySelectorAll("[data-ms-arama-temizle]");
    if (!input || !panel) return;

    const paneliAc = () => {
      panel.classList.add("ms-ana-navigasyon-arama-panel-acik");
      aramaAlani.classList.add("ms-ana-navigasyon-arama-acik");
      if (panelInput) panelInput.value = input.value;
    };
    const paneliKapat = () => {
      panel.classList.remove("ms-ana-navigasyon-arama-panel-acik");
      aramaAlani.classList.remove("ms-ana-navigasyon-arama-acik");
    };

    input.addEventListener("focus", paneliAc);
    input.addEventListener("click", paneliAc);
    input.addEventListener("input", () => {
      if (panelInput) panelInput.value = input.value;
      temizleButonlari.forEach((b) => { b.hidden = !input.value; });
      window.msAramaSonuclariniGetir?.(input.value.trim());
    });
    panelInput?.addEventListener("input", () => {
      input.value = panelInput.value;
      temizleButonlari.forEach((b) => { b.hidden = !panelInput.value; });
      window.msAramaSonuclariniGetir?.(panelInput.value.trim());
    });

    temizleButonlari.forEach((b) => {
      b.addEventListener("click", (event) => {
        event.preventDefault();
        input.value = ""; if (panelInput) panelInput.value = "";
        b.hidden = true;
        window.msAramaSonuclariniGetir?.("");
        (panel.classList.contains("ms-ana-navigasyon-arama-panel-acik") ? panelInput : input)?.focus();
      });
    });

    kapat?.addEventListener("click", () => { paneliKapat(); input.blur(); });
    document.addEventListener("pointerdown", (event) => {
      if (!aramaAlani.contains(event.target)) paneliKapat();
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") { paneliKapat(); input.blur(); }
    });
  });
})();

// ─────────────────────────────────────────────────────────
// Sepet dropdown aç/kapa (nav'daki sepet ikonu).
// ─────────────────────────────────────────────────────────
(() => {
  document.querySelectorAll("[data-ms-sepet-menu]").forEach((menu) => {
    const tetikleyici = menu.querySelector("[data-ms-sepet-menu-tetikleyici]");
    const panel = menu.querySelector("[data-ms-sepet-menu-panel]");
    if (!tetikleyici) return;

    const menuKapat = () => {
      menu.classList.remove("ms-ana-navigasyon-sepet-acik");
      tetikleyici.setAttribute("aria-expanded", "false");
    };
    const menuToggle = () => {
      const acik = menu.classList.toggle("ms-ana-navigasyon-sepet-acik");
      tetikleyici.setAttribute("aria-expanded", acik.toString());
    };

    tetikleyici.addEventListener("pointerdown", (event) => event.stopPropagation());
    tetikleyici.addEventListener("click", (event) => { event.preventDefault(); event.stopPropagation(); menuToggle(); });
    panel?.addEventListener("pointerdown", (event) => event.stopPropagation());
    panel?.addEventListener("click", (event) => event.stopPropagation());
    document.addEventListener("pointerdown", (event) => { if (!menu.contains(event.target)) menuKapat(); });
    document.addEventListener("keydown", (event) => { if (event.key === "Escape") { menuKapat(); tetikleyici.blur(); } });

    window.msSepetMenuAc = () => { menu.classList.add("ms-ana-navigasyon-sepet-acik"); tetikleyici.setAttribute("aria-expanded", "true"); };
    window.msSepetMenuKapat = menuKapat;
    window.msSepetMenuToggle = menuToggle;
  });
})();

// ─────────────────────────────────────────────────────────
// Mağaza mega menü (masaüstü) — hover/click ile kategori değişimi.
// DOM yapısı app.js tarafından initNav()'da doğrudan doğru sırayla üretilir
// (sol kolon + içerik panelleri ayrı), Misharix'teki gibi reparent gerekmez.
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-magaza-menu]").forEach((menu) => {
      if (menu.dataset.msMagazaMenuHazir === "true") return;
      const megaMenu = menu.querySelector("[data-ms-magaza-mega-menu]");
      const anaMenuLink = menu.querySelector(".ms-magaza-menu-tum > .ms-magaza-menu-link");
      if (!megaMenu || !anaMenuLink) return;
      menu.dataset.msMagazaMenuHazir = "true";

      const ustLinkler = menu.querySelectorAll("[data-ms-magaza-menu-link]");
      const solLinkler = megaMenu.querySelectorAll("[data-ms-magaza-kategori]");

      const menuAc = () => menu.classList.add("ms-magaza-mega-menu-acik");
      const kategoriAc = (kategori) => {
        menu.querySelectorAll(".ms-magaza-mega-icerik").forEach((p) => p.classList.remove("ms-magaza-mega-icerik-aktif"));
        menu.querySelectorAll(".ms-magaza-mega-sol-link").forEach((l) => l.classList.remove("ms-magaza-mega-sol-link-aktif"));
        ustLinkler.forEach((l) => l.classList.toggle("ms-magaza-menu-link-aktif", l.dataset.msMagazaMenuLink === kategori));

        const hedefPanel = megaMenu.querySelector(`[data-ms-magaza-panel="${kategori}"]`);
        const hedefGrup = menu.querySelector(`[data-ms-magaza-kategori-grubu="${kategori}"]`);
        hedefPanel?.classList.add("ms-magaza-mega-icerik-aktif");
        hedefGrup?.querySelector(".ms-magaza-mega-sol-link")?.classList.add("ms-magaza-mega-sol-link-aktif");
      };
      const kategoriKapat = () => {
        menu.classList.remove("ms-magaza-mega-menu-acik");
        menu.querySelectorAll(".ms-magaza-mega-icerik").forEach((p) => p.classList.remove("ms-magaza-mega-icerik-aktif"));
        menu.querySelectorAll(".ms-magaza-mega-sol-link").forEach((l) => l.classList.remove("ms-magaza-mega-sol-link-aktif"));
        ustLinkler.forEach((l) => l.classList.remove("ms-magaza-menu-link-aktif"));
      };

      anaMenuLink.addEventListener("mouseenter", menuAc);
      anaMenuLink.addEventListener("click", (event) => { event.preventDefault(); menuAc(); });

      ustLinkler.forEach((link) => {
        const kategori = link.dataset.msMagazaMenuLink;
        link.addEventListener("mouseenter", () => { menuAc(); kategoriAc(kategori); });
        link.addEventListener("click", (event) => { event.preventDefault(); menuAc(); kategoriAc(kategori); });
      });
      solLinkler.forEach((link) => {
        const kategori = link.dataset.msMagazaKategori;
        link.addEventListener("mouseenter", () => kategoriAc(kategori));
        link.addEventListener("click", (event) => { event.preventDefault(); kategoriAc(kategori); });
      });

      menu.addEventListener("mouseleave", kategoriKapat);
      document.addEventListener("pointerdown", (event) => { if (!menu.contains(event.target)) kategoriKapat(); });
    });
  };

  window.msMagazaMenuBaslat = baslat;
  window.msRegisterPageModule("magaza-menu", baslat);
})();

// ─────────────────────────────────────────────────────────
// Footer akordiyonu (mobilde kolonlar kapalı başlar, tıklayınca açılır).
// ─────────────────────────────────────────────────────────
(() => {
  const baslat = (kok = document) => {
    kok.querySelectorAll("[data-ms-footer-akordiyon]").forEach((kolon) => {
      if (kolon.dataset.msFooterAkordiyonHazir === "true") return;
      const tetikleyici = kolon.querySelector("[data-ms-footer-akordiyon-tetikleyici]");
      const icerik = kolon.querySelector("[data-ms-footer-akordiyon-icerik]");
      if (!tetikleyici || !icerik) return;
      kolon.dataset.msFooterAkordiyonHazir = "true";

      tetikleyici.addEventListener("click", () => {
        const acik = kolon.classList.toggle("ms-footer-akordiyon-acik");
        tetikleyici.setAttribute("aria-expanded", acik.toString());
      });
    });
  };

  window.msRegisterPageModule("footer-akordiyon", baslat);
})();

// ─────────────────────────────────────────────────────────
// INIT — sayfa yüklendiğinde tüm modülleri çalıştır.
// ─────────────────────────────────────────────────────────
if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", () => window.msRunPageModules(), { once: true });
} else {
  window.msRunPageModules();
}
