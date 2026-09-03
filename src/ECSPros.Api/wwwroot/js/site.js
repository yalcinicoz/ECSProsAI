// Misharix global UI davranislari.
// Bu dosyada veri uretimi veya sayfaya ozel fetch/listeleme mantigi tutulmaz.

// Kapali koleksiyon modallarini ilk kullanimda DOM'a alir.
(() => {
    const koleksiyonModallariniHazirla = () => {
        const sablon = document.querySelector("[data-ms-koleksiyon-modallari-sablon]");

        if (!(sablon instanceof HTMLTemplateElement)) {
            return;
        }

        sablon.parentNode?.insertBefore(sablon.content.cloneNode(true), sablon);
        sablon.remove();
        window.msOzelSelectleriBaslat?.(document);
        window.msKoleksiyonAkisModallariBaslat?.();
        window.msKoleksiyonModallariBaslat?.(document);
        window.msModalAsagiSurukleBaslat?.(document);
    };

    document.addEventListener("click", (event) => {
        if (event.target.closest("[data-ms-urun-koleksiyon], [data-ms-koleksiyon-modal-ac], [data-ms-urun-detay-koleksiyon]")) {
            koleksiyonModallariniHazirla();
        }
    }, true);
})();

// Kupon sayaçları için ortak geri sayım davranışı.
(() => {
    const sayiYaz = (deger) => String(Math.max(0, deger)).padStart(2, "0");

    const sayaciGuncelle = (sayac) => {
        const hedefMetin = sayac.dataset.msKuponHedef;
        const hedefTarih = hedefMetin ? new Date(hedefMetin) : null;

        if (!hedefTarih || Number.isNaN(hedefTarih.getTime())) {
            return;
        }

        const kalanMs = Math.max(0, hedefTarih.getTime() - Date.now());
        const toplamSaniye = Math.floor(kalanMs / 1000);
        const gun = Math.floor(toplamSaniye / 86400);
        const saat = Math.floor((toplamSaniye % 86400) / 3600);
        const dakika = Math.floor((toplamSaniye % 3600) / 60);
        const saniye = toplamSaniye % 60;

        const gunAlani = sayac.querySelector("[data-ms-kupon-gun]");
        const saatAlani = sayac.querySelector("[data-ms-kupon-saat]");
        const dakikaAlani = sayac.querySelector("[data-ms-kupon-dakika]");
        const saniyeAlani = sayac.querySelector("[data-ms-kupon-saniye]");

        if (gunAlani) {
            gunAlani.textContent = sayiYaz(gun);
        }

        if (saatAlani) {
            saatAlani.textContent = sayiYaz(saat);
        }

        if (dakikaAlani) {
            dakikaAlani.textContent = sayiYaz(dakika);
        }

        if (saniyeAlani) {
            saniyeAlani.textContent = sayiYaz(saniye);
        }

        sayac.classList.toggle("ms-kupon-sayac-bitti", kalanMs <= 0);
    };

    window.msKuponSayacBaslat = (kok = document) => {
        if (!kok?.querySelectorAll) {
            return;
        }

        kok.querySelectorAll("[data-ms-kupon-sayac]").forEach((sayac) => {
            if (sayac.dataset.msKuponSayacHazir === "true") {
                sayaciGuncelle(sayac);
                return;
            }

            sayac.dataset.msKuponSayacHazir = "true";
            sayaciGuncelle(sayac);
            window.setInterval(() => sayaciGuncelle(sayac), 1000);
        });
    };

    const baslat = () => window.msKuponSayacBaslat(document);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", baslat, { once: true });
    } else {
        baslat();
    }
})();

// Mobil modallarda ust alandan asagi surukleyerek kapatma davranisi.
(() => {
    const mobilEslesmesi = window.matchMedia("(max-width: 639px)");
    // Story modali cek-kapat disinda (kullanici karari 2026-07-17): tutamac seridi story
    // cercevesinde siyah bant olusturuyor ve kapat butonunu yerinden tasiyordu.
    const modalSecici = ".ms-giris-modal, .ms-ornek-modal, [data-ms-giris-modal], [data-ms-kayit-modal], [data-ms-ornek-modal], [data-ms-tckn-modal]";
    const kutuSecici = ".ms-giris-modal-kutu, .ms-ornek-modal-kutu, .ms-urun-detay-resim-modal-kutu";
    const kapaticiSecici = "[data-ms-giris-modal-kapat], [data-ms-kayit-modal-kapat], [data-ms-ornek-modal-kapat], [data-ms-belge-modal-kapat], [data-ms-tckn-modal-kapat], [data-ms-siparis-detay-kapat], [data-ms-kargo-takip-kapat], [data-ms-hazirlik-durumu-kapat], [data-ms-iade-sayfasi-modal-kapat], [data-ms-iade-kodu-kapat], [data-ms-story-kapat], [data-ms-urun-detay-resim-modal-kapat], [data-ms-urun-paylas-kapat], [data-ms-gorsel-arama-kapat], [data-ms-koleksiyon-modal-kapat], [data-ms-koleksiyon-secim-modal-kapat], [data-ms-koleksiyon-varolan-modal-kapat], [data-ms-koleksiyon-yeni-ozet-modal-kapat], [data-ms-onay-red-modal-kapat], [data-ms-fatura-modal-kapat], [data-ms-iade-sms-kapat], [data-ms-iade-hata-kapat], [data-ms-adres-modal-kapat], [data-ms-kupon-modal-kapat], [data-ms-sozlesme-modal-kapat], [data-ms-sepet-sil-modal-kapat], [data-ms-yorum-kriter-kapat], [data-ms-yorum-yap-kapat], .ms-giris-modal-kapat, .ms-ornek-modal-kapat";
    const suruklemeAlaniSecici = [
        "[data-ms-modal-surukleme-alani]",
        ".ms-giris-modal-baslik-alani",
        ".ms-giris-modal-baslik",
        ".ms-giris-modal-aciklama",
        ".ms-ornek-modal-baslik",
        ".ms-ornek-modal-aciklama",
        ".ms-modal-ornek-ikon"
    ].join(", ");
    const suruklemeBaslikYuksekligi = 88;
    const kapatmaMesafesi = 96;

    const etkilesimliHedefMi = (hedef) => hedef instanceof Element
        && Boolean(hedef.closest("button, a, input, select, textarea, label, [role='button'], [data-ms-modal-surukleme-yok]"));

    const modalAcmaTetikleyicisiMi = (hedef) => {
        if (!(hedef instanceof Element)) {
            return false;
        }

        const tetikleyici = hedef.closest("button, a, [role='button'], [data-ms-ornek-modal-ac], [data-ms-giris-modal-ac], [data-ms-kayit-modal-ac]");

        if (!tetikleyici) {
            return false;
        }

        return Array.from(tetikleyici.attributes).some((attribute) => {
            const ad = attribute.name.toLowerCase();
            return ad.startsWith("data-ms-") && ad.includes("modal") && (ad.endsWith("-ac") || ad.includes("-modal-ac"));
        });
    };

    const modalViewportYuksekliginiGuncelle = () => {
        const viewportYuksekligi = Math.round(window.visualViewport?.height || window.innerHeight);

        if (viewportYuksekligi > 0) {
            document.documentElement.style.setProperty("--ms-modal-viewport-yuksekligi", `${viewportYuksekligi}px`);
        }
    };

    const modalKapat = (modal) => {
        const kapatici = modal.querySelector(kapaticiSecici)
            || Array.from(modal.querySelectorAll("[data-ms-ornek-modal-kapat], [class*='modal-kapat'], [data-ms-giris-modal-kapat], [data-ms-kayit-modal-kapat], button, a"))
                .find((oge) => Array.from(oge.attributes).some((attribute) => attribute.name.toLowerCase().startsWith("data-ms-") && attribute.name.toLowerCase().includes("kapat")));
        kapatici?.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    };

    const suruklemeTutamaciEkle = (kutu) => {
        if (!kutu.matches(kutuSecici)) {
            return;
        }

        let tutamacAlani = kutu.querySelector(":scope > .ms-modal-surukleme-tutamac-alani");

        if (!tutamacAlani) {
            tutamacAlani = document.createElement("div");
            tutamacAlani.className = "ms-modal-surukleme-tutamac-alani";
            tutamacAlani.dataset.msModalSuruklemeAlani = "";

            const tutamac = document.createElement("span");
            tutamac.className = "ms-modal-surukleme-tutamac";
            tutamac.setAttribute("aria-hidden", "true");
            tutamacAlani.append(tutamac);
            kutu.prepend(tutamacAlani);
        }

        const kapatmaButonu = kutu.querySelector(kapaticiSecici);
        if (kapatmaButonu && kapatmaButonu.parentElement !== tutamacAlani) {
            tutamacAlani.append(kapatmaButonu);
        }
    };

    window.msModalAsagiSurukleBaslat = (kok = document) => {
        if (!kok?.querySelectorAll) {
            return;
        }

        kok.querySelectorAll(modalSecici).forEach((modal) => {
            if (modal.dataset.msModalAsagiSurukleHazir === "true") {
                return;
            }

            const kutu = modal.querySelector(kutuSecici);

            if (!kutu) {
                return;
            }

            suruklemeTutamaciEkle(kutu);

            modal.dataset.msModalAsagiSurukleHazir = "true";

            let surukleniyor = false;
            let baslangicY = 0;
            let sonKayma = 0;
            let hedefKayma = 0;
            let animasyonId = 0;

            const kaymayiUygula = () => {
                animasyonId = 0;
                kutu.style.transform = hedefKayma > 0 ? `translateY(${hedefKayma}px)` : "";
                modal.style.setProperty("--ms-modal-kaplama-opaklik", String(Math.max(0.2, 0.45 - Math.min(hedefKayma / 420, 0.22))));
            };

            const kaymayiPlanla = () => {
                if (animasyonId) {
                    return;
                }

                animasyonId = window.requestAnimationFrame(kaymayiUygula);
            };

            const kaymayiSifirla = () => {
                if (animasyonId) {
                    window.cancelAnimationFrame(animasyonId);
                    animasyonId = 0;
                }

                hedefKayma = 0;
                sonKayma = 0;
                kutu.style.transform = "";
                kutu.style.transition = "";
                kutu.style.willChange = "";
                modal.style.removeProperty("--ms-modal-kaplama-opaklik");
                modal.classList.remove("ms-modal-asagi-surukleniyor");
            };

            kutu.addEventListener("pointerdown", (event) => {
                const suruklemeAlani = event.target instanceof Element
                    ? event.target.closest(suruklemeAlaniSecici)
                    : null;
                const isaretliSuruklemeAlani = suruklemeAlani && kutu.contains(suruklemeAlani);
                const etkilesimliHedef = etkilesimliHedefMi(event.target);

                if (!mobilEslesmesi.matches || etkilesimliHedef) {
                    return;
                }

                const kutuSiniri = kutu.getBoundingClientRect();
                const ustMesafe = event.clientY - kutuSiniri.top;

                if (!isaretliSuruklemeAlani) {
                    if ((ustMesafe < 0 || ustMesafe > suruklemeBaslikYuksekligi) || kutu.scrollTop > 0) {
                        return;
                    }
                }

                surukleniyor = true;
                baslangicY = event.clientY;
                sonKayma = 0;
                hedefKayma = 0;
                kutu.style.transition = "none";
                kutu.style.willChange = "transform";
                modal.classList.add("ms-modal-asagi-surukleniyor");
                event.preventDefault();
                kutu.setPointerCapture?.(event.pointerId);
            });

            kutu.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                const fark = event.clientY - baslangicY;

                if (fark <= 0) {
                    sonKayma = 0;
                    hedefKayma = 0;
                    kaymayiPlanla();
                    return;
                }

                const enFazlaKayma = Math.max(window.innerHeight || 0, kutu.offsetHeight || 0, 320);
                sonKayma = Math.min(fark, enFazlaKayma);
                hedefKayma = sonKayma;
                event.preventDefault();
                kaymayiPlanla();
            });

            const suruklemeyiBitir = (event) => {
                if (!surukleniyor) {
                    return;
                }

                surukleniyor = false;

                if (event?.pointerId && kutu.hasPointerCapture?.(event.pointerId)) {
                    kutu.releasePointerCapture(event.pointerId);
                }

                if (animasyonId) {
                    window.cancelAnimationFrame(animasyonId);
                    animasyonId = 0;
                }

                if (sonKayma >= kapatmaMesafesi) {
                    kutu.style.transition = "transform 220ms ease";
                    kutu.style.transform = "translateY(110%)";
                    window.setTimeout(() => {
                        modalKapat(modal);
                        kaymayiSifirla();
                    }, 180);
                    return;
                }

                kutu.style.transition = "transform 220ms ease";
                kutu.style.transform = "";
                modal.style.removeProperty("--ms-modal-kaplama-opaklik");
                window.setTimeout(kaymayiSifirla, 230);
            };

            kutu.addEventListener("pointerup", suruklemeyiBitir);
            kutu.addEventListener("pointercancel", suruklemeyiBitir);
        });
    };

    document.addEventListener("pointerup", (event) => {
        if (modalAcmaTetikleyicisiMi(event.target)) {
            window.msModalAsagiSurukleBaslat(document);
            modalViewportYuksekliginiGuncelle();
        }
    }, true);

    document.addEventListener("click", (event) => {
        if (modalAcmaTetikleyicisiMi(event.target)) {
            window.msModalAsagiSurukleBaslat(document);
            modalViewportYuksekliginiGuncelle();
            window.setTimeout(() => window.msModalAsagiSurukleBaslat(document), 0);
            window.setTimeout(modalViewportYuksekliginiGuncelle, 40);
        }
    }, true);

    const modalAcikMi = (modal) => modal.classList.contains("ms-giris-modal-acik")
        || modal.classList.contains("ms-ornek-modal-acik")
        || !modal.classList.contains("ms-gizli") && modal.getAttribute("aria-hidden") === "false";

    if (typeof MutationObserver !== "undefined") {
        const modalGozlemci = new MutationObserver((kayitlar) => {
            if (!mobilEslesmesi.matches) {
                return;
            }

            if (kayitlar.some((kayit) => kayit.target instanceof Element && kayit.target.matches(modalSecici) && modalAcikMi(kayit.target))) {
                modalViewportYuksekliginiGuncelle();
            }
        });

        const modalGozle = (kok = document) => {
            kok.querySelectorAll(modalSecici).forEach((modal) => {
                modalGozlemci.observe(modal, {
                    attributes: true,
                    attributeFilter: ["class", "aria-hidden", "hidden"]
                });
            });
        };

        modalGozle(document);
        document.addEventListener("DOMContentLoaded", () => modalGozle(document), { once: true });

        const yeniModalGozlemci = new MutationObserver((kayitlar) => {
            const modalEklendi = kayitlar.some((kayit) => Array.from(kayit.addedNodes).some((dugum) => dugum instanceof Element
                && (dugum.matches(modalSecici) || dugum.querySelector(modalSecici))));

            if (!modalEklendi) {
                return;
            }

            window.msModalAsagiSurukleBaslat(document);
            modalGozle(document);
        });

        yeniModalGozlemci.observe(document.documentElement, { childList: true, subtree: true });
    }

    window.msModalAsagiSurukleBaslat(document);
    modalViewportYuksekliginiGuncelle();
    window.visualViewport?.addEventListener("resize", modalViewportYuksekliginiGuncelle, { passive: true });
    window.visualViewport?.addEventListener("scroll", modalViewportYuksekliginiGuncelle, { passive: true });
    window.addEventListener("orientationchange", modalViewportYuksekliginiGuncelle, { passive: true });
    document.addEventListener("DOMContentLoaded", () => window.msModalAsagiSurukleBaslat(document), { once: true });
})();

// Mobil alt bar Kategoriler kisa yolu ana mobil menuyu acar.
(() => {
    document.addEventListener("click", (event) => {
        const hedef = event.target instanceof Element
            ? event.target.closest(".ms-mobil-alt-bar-link")
            : null;

        if (!hedef?.querySelector(".ms-mobil-alt-bar-ikon-kategoriler")) {
            return;
        }

        const mobilMenuAcButonu = document.querySelector("[data-ms-mobil-menu-ac]");

        if (!mobilMenuAcButonu) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        mobilMenuAcButonu.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    });
})();

// ProjeElementleri scroll konumunu reload sonrasi kaldigi yerden devam ettirir.
(() => {
    if (!window.location.pathname.toLowerCase().includes("/projeelementleri")) {
        return;
    }

    const scrollAnahtari = `ms-proje-elementleri-scroll:${window.location.pathname}${window.location.search}`;
    let restoreDenemesi = 0;
    let scrollKaydiAktif = false;
    let scrollKayitZamani;

    const scrollKaydet = () => {
        if (!scrollKaydiAktif) {
            return;
        }

        window.clearTimeout(scrollKayitZamani);
        scrollKayitZamani = window.setTimeout(() => {
            sessionStorage.setItem(scrollAnahtari, String(window.scrollY || window.pageYOffset || 0));
        }, 120);
    };

    const scrollGeriYukle = () => {
        const hedef = Number(sessionStorage.getItem(scrollAnahtari) || 0);

        if (!hedef || window.location.hash) {
            return;
        }

        window.scrollTo(0, hedef);
        restoreDenemesi += 1;

        if (restoreDenemesi < 12 && Math.abs((window.scrollY || window.pageYOffset || 0) - hedef) > 4) {
            window.setTimeout(scrollGeriYukle, 120);
        }
    };

    window.addEventListener("scroll", scrollKaydet, { passive: true });
    window.addEventListener("pagehide", () => {
        sessionStorage.setItem(scrollAnahtari, String(window.scrollY || window.pageYOffset || 0));
    });
    window.addEventListener("pageshow", () => {
        const gezinme = performance.getEntriesByType?.("navigation")?.[0];

        if (gezinme?.type === "reload") {
            window.requestAnimationFrame(scrollGeriYukle);
            window.setTimeout(scrollGeriYukle, 200);
            window.setTimeout(scrollGeriYukle, 600);
            window.setTimeout(() => {
                scrollKaydiAktif = true;
            }, 900);
        } else {
            scrollKaydiAktif = true;
        }
    });

    window.msProjeElementleriScrollGeriYukle = scrollGeriYukle;
})();

// Sayfa modulu registry davranisi.
(() => {
    const msPageModulDurum = new WeakMap();
    const msPageModuller = window.msPageModules || {};

    window.msPageModules = msPageModuller;

    window.msRegisterPageModule = (modulAdi, baslatici) => {
        if (!modulAdi || typeof baslatici !== "function") {
            return;
        }

        msPageModuller[String(modulAdi).trim()] = baslatici;
    };

    window.msRunPageModules = (kok = document) => {
        if (!kok?.querySelectorAll) {
            return;
        }

        const alanlar = [kok, ...Array.from(kok.querySelectorAll("[data-ms-page-module],[data-ms-page-script]"))];
        const tekilAlanlar = Array.from(new Set(alanlar));

        tekilAlanlar.forEach((modulAlani) => {
            if (!modulAlani?.dataset) {
                return;
            }

            const modulAdlari = `${modulAlani.dataset.msPageModule || modulAlani.dataset.msPageScript || ""}`
                .split(",")
                .map((ad) => ad.trim())
                .filter(Boolean);

            if (!modulAdlari.length) {
                return;
            }

            let calisanModuller = msPageModulDurum.get(modulAlani);
            if (!calisanModuller) {
                calisanModuller = new Set();
                msPageModulDurum.set(modulAlani, calisanModuller);
            }

            modulAdlari.forEach((modulAdi) => {
                if (calisanModuller.has(modulAdi)) {
                    return;
                }

                const baslatici = msPageModuller[modulAdi];
                if (typeof baslatici !== "function") {
                    return;
                }

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

// Urun kartlari ve detay sayfasindaki video rozetlerini hover/tiklama ile yonetir.
(() => {
    const videoAlanlariniBul = (kok) => {
        const alanlar = [];

        if (kok?.matches?.("[data-ms-urun-video]")) {
            alanlar.push(kok);
        }

        kok?.querySelectorAll?.("[data-ms-urun-video]").forEach((alan) => alanlar.push(alan));
        return Array.from(new Set(alanlar));
    };

    const videoAlaniniKapat = (videoAlani, zorlama = false) => {
        if (!videoAlani || (!zorlama && videoAlani.matches(":hover"))) {
            return;
        }

        const video = videoAlani.querySelector("video");
        const tetikleyici = videoAlani.querySelector(".ms-urun-video-rozeti");
        videoAlani.classList.remove("ms-urun-video-alani-acik");
        videoAlani.classList.toggle("ms-urun-video-alani-kapali", zorlama);
        tetikleyici?.setAttribute("aria-expanded", "false");

        window.setTimeout(() => {
            if (!zorlama && videoAlani.matches(":hover")) {
                return;
            }

            video?.pause?.();
            if (video) {
                video.currentTime = 0;
            }
        }, zorlama ? 0 : 90);
    };

    const digerVideoAlanlariniKapat = (aktifAlan = null) => {
        document.querySelectorAll("[data-ms-urun-video].ms-urun-video-alani-acik").forEach((videoAlani) => {
            if (videoAlani !== aktifAlan) {
                videoAlaniniKapat(videoAlani, true);
            }
        });
    };

    const videoAlaniniHazirla = (videoAlani) => {
        if (!videoAlani || videoAlani.dataset.msUrunVideoHazir === "true") {
            return;
        }

        const video = videoAlani.querySelector("video");
        const tetikleyici = videoAlani.querySelector(".ms-urun-video-rozeti");

        if (!video || !tetikleyici) {
            return;
        }

        videoAlani.dataset.msUrunVideoHazir = "true";
        video.muted = true;
        video.playsInline = true;
        video.setAttribute("muted", "");
        video.setAttribute("playsinline", "");
        tetikleyici.setAttribute("aria-expanded", "false");

        const oynat = (sabitAc = false) => {
            videoAlani.classList.remove("ms-urun-video-alani-kapali");

            if (sabitAc) {
                digerVideoAlanlariniKapat(videoAlani);
                videoAlani.classList.add("ms-urun-video-alani-acik");
                tetikleyici.setAttribute("aria-expanded", "true");
            }

            if (video.getAttribute("preload") === "none") {
                video.preload = "auto";
                video.setAttribute("preload", "auto");
            }

            if (!video.paused && !video.ended) {
                return;
            }

            video.play().catch(() => {});
        };

        const toparla = () => {
            window.setTimeout(() => {
                if ((videoAlani.matches(":hover") || videoAlani.classList.contains("ms-urun-video-alani-acik")) && video.paused) {
                    video.play().catch(() => {});
                }
            }, 160);
        };

        videoAlani.addEventListener("pointerenter", () => oynat(false));
        videoAlani.addEventListener("pointerleave", () => {
            videoAlani.classList.remove("ms-urun-video-alani-kapali");

            if (!videoAlani.classList.contains("ms-urun-video-alani-acik")) {
                videoAlaniniKapat(videoAlani);
            }
        });
        video.addEventListener("waiting", toparla);
        video.addEventListener("stalled", toparla);
        video.addEventListener("suspend", toparla);
        // 2026-08-14: kaynak yüklenemiyorsa (ör. video CDN'i erişilemez/DNS yok) hover'da
        // BOŞ KUTU göstermek yerine tooltip kapatılır ve rozet tamamen gizlenir.
        video.addEventListener("error", () => {
            videoAlaniniKapat(videoAlani, true);
            const etiketAlani = videoAlani.closest(".ms-urun-gorsel-etiketleri") || videoAlani;
            etiketAlani.hidden = true;
        });
        tetikleyici.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();

            if (videoAlani.classList.contains("ms-urun-video-alani-acik")) {
                videoAlaniniKapat(videoAlani, true);
                return;
            }

            oynat(true);
        });
    };

    window.msUrunVideoDavranisiHazirla = (kok = document) => {
        videoAlanlariniBul(kok).forEach(videoAlaniniHazirla);
    };

    document.addEventListener("click", (event) => {
        if (!event.target.closest("[data-ms-urun-video]")) {
            digerVideoAlanlariniKapat();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            digerVideoAlanlariniKapat();
        }
    });

    window.msUrunVideoDavranisiHazirla(document);
})();

// Opt-in infinite scroll davranisi.
(() => {
    const hazirAlanlar = new WeakSet();
    const lazyInfiniteSecici = ".lazy-infinite-on";

    const lazyInfiniteAktifMi = (alan) => Boolean(alan?.closest?.(lazyInfiniteSecici));

    const aktifInfiniteAdaylariBul = (kok, secici) => {
        const adaylar = [];

        if (kok?.matches?.(secici)) {
            adaylar.push(kok);
        }

        kok?.querySelectorAll?.(secici).forEach((alan) => {
            adaylar.push(alan);
        });

        return adaylar.filter(lazyInfiniteAktifMi);
    };

    const sayiOku = (deger, varsayilan) => {
        const sayi = Number.parseInt(deger, 10);
        return Number.isFinite(sayi) ? sayi : varsayilan;
    };

    const configOku = (alan) => {
        const configAdi = alan.dataset.msInfiniteConfig?.trim();
        const globalConfig = configAdi ? window.msInfiniteConfigs?.[configAdi] : null;
        const inlineAyar = alan.dataset.msInfiniteToplam || alan.dataset.msInfiniteIlk || alan.dataset.msInfiniteAdet || alan.dataset.msInfiniteEsik || alan.dataset.msInfiniteStateKey || alan.dataset.msInfiniteSadeceIlk;

        if (configAdi && !globalConfig) {
            return null;
        }

        if (!configAdi && !inlineAyar) {
            return null;
        }

        return {
            toplam: sayiOku(globalConfig?.toplam ?? alan.dataset.msInfiniteToplam, 100),
            ilk: sayiOku(globalConfig?.ilk ?? alan.dataset.msInfiniteIlk, 20),
            adet: sayiOku(globalConfig?.adet ?? alan.dataset.msInfiniteAdet, 20),
            esik: Number.parseFloat(globalConfig?.esik ?? alan.dataset.msInfiniteEsik ?? "0.8"),
            stateKey: globalConfig?.stateKey || alan.dataset.msInfiniteStateKey || "",
            sadeceIlkYukle: (globalConfig?.sadeceIlkYukle ?? alan.dataset.msInfiniteSadeceIlk) === true || alan.dataset.msInfiniteSadeceIlk === "true",
            kartHazirla: typeof globalConfig?.kartHazirla === "function" ? globalConfig.kartHazirla : null,
            sonra: typeof globalConfig?.sonra === "function" ? globalConfig.sonra : null
        };
    };

    // 2026-08-14: dışa açık — liste sayfasının YUKARI yönlü (önceki sayfa) yükleyicisi de
    // klonladığı kartlara tıklama davranışını bağlamak zorunda; bağlanmayınca ?page=N ile
    // gelinen sayfada üste eklenen kartlar tıklanamıyordu.
    const kartTiklamasiniHazirla = (kart) => {
        if (!kart || kart.dataset.msKartLinkHazir === "true") {
            return;
        }

        kart.dataset.msKartLinkHazir = "true";
        kart.addEventListener("click", (event) => {
            if (event.target.closest("a, button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], [data-ms-urun-video], .ms-urun-video-alani, .ms-urun-renk-tooltip-alani, .ms-urun-renk-rozet")) {
                return;
            }

            const link = kart.querySelector("[data-ms-kart-link]");
            if (!link) {
                return;
            }
            // Sentetik link.click() modifier tasimaz — Ctrl/Cmd+tik yeni sekmede acilir (2026-07-17)
            if (event.ctrlKey || event.metaKey) {
                window.open(link.href, "_blank", "noopener");
                return;
            }
            link.click();
        });
    };
    window.msKartTiklamasiniHazirla = kartTiklamasiniHazirla;

    const templateKartOlustur = (template, sira, config) => {
        const parca = template.content.cloneNode(true);

        parca.querySelectorAll("img").forEach((img) => {
            const src = img.getAttribute("src");
            const lazyDisi =
                img.classList.contains("no-lazy") ||
                img.classList.contains("ms-urun-kampanya-etiketi") ||
                img.closest(".ms-urun-gorsel-etiketleri");

            if (src && !lazyDisi) {
                img.dataset.msLazySrc = src;
                img.removeAttribute("src");
            }

            if (img.classList.contains("ms-urun-gorsel")) {
                img.dataset.msLazySkeleton = "true";
            }
        });

        parca.querySelectorAll("video").forEach((video) => {
            video.preload = "none";
            video.setAttribute("preload", "none");
        });

        parca.querySelectorAll("[data-ms-kart-link-alani]").forEach((kart) => {
            kart.dataset.msInfiniteKart = sira.toString();
            kartTiklamasiniHazirla(kart);
        });

        config.kartHazirla?.(parca, sira);
        return parca;
    };

    const infiniteAlaniBaslat = (alan) => {
        if (!alan || hazirAlanlar.has(alan)) {
            return;
        }

        const liste = alan.querySelector("[data-ms-infinite-liste]");
        const template = alan.querySelector("[data-ms-infinite-template]");
        const yukleniyor = alan.querySelector("[data-ms-infinite-yukleniyor]");

        if (!liste || !template) {
            return;
        }

        hazirAlanlar.add(alan);

        const config = configOku(alan);
        if (!config) {
            return;
        }

        const stateKey = config.stateKey || `ms-infinite-scroll:${window.location.pathname}${window.location.search}:${alan.closest("[data-panel]")?.dataset.panel || "genel"}:${config.toplam}`;
        const kayitliAdet = Number(sessionStorage.getItem(stateKey) || 0);
        const baslangicAdedi = config.sadeceIlkYukle ? config.ilk : Math.min(Math.max(config.ilk, kayitliAdet), config.toplam);
        let uretilen = 0;
        let yuklemeVar = false;

        const kartEkle = (adet = config.adet) => {
            if (yuklemeVar || uretilen >= config.toplam) {
                return;
            }

            yuklemeVar = true;
            yukleniyor?.classList.add("ms-aktif");

            window.setTimeout(() => {
                const parca = document.createDocumentFragment();
                const hedef = Math.min(uretilen + adet, config.toplam);

                for (let sira = uretilen + 1; sira <= hedef; sira += 1) {
                    parca.appendChild(templateKartOlustur(template, sira, config));
                }

                liste.appendChild(parca);
                uretilen = hedef;
                sessionStorage.setItem(stateKey, String(uretilen));
                yuklemeVar = false;
                yukleniyor?.classList.remove("ms-aktif");
                window.msUrunKartDavranislariYenile?.(liste);
                window.msLazyLoadYenile?.(liste);
                config.sonra?.(liste, uretilen);
            }, 80);
        };

        const kontrolEt = () => {
            if (yuklemeVar || uretilen >= config.toplam) {
                return;
            }

            const listeRect = liste.getBoundingClientRect();
            const listeBaslangic = listeRect.top + window.scrollY;
            const listeYukseklik = Math.max(liste.offsetHeight, 1);
            const gorunenAlt = window.scrollY + window.innerHeight;
            const listeIlerlemesi = (gorunenAlt - listeBaslangic) / listeYukseklik;

            if (listeIlerlemesi >= config.esik) {
                kartEkle();
            }
        };

        kartEkle(baslangicAdedi);
        window.addEventListener("scroll", kontrolEt, { passive: true });
        window.addEventListener("resize", kontrolEt);

        if (!config.sadeceIlkYukle) {
            window.setTimeout(kontrolEt, 140);
        }
    };

    const baslat = (kok = document) => {
        if (!kok?.querySelectorAll) {
            return;
        }

        aktifInfiniteAdaylariBul(kok, "[data-ms-infinite-scroll]").forEach(infiniteAlaniBaslat);
    };

    window.msInfiniteScrollBaslat = baslat;
    window.msRegisterPageModule?.("infinite-scroll", baslat);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => {
            baslat();
            window.msRunPageModules?.();
        }, { once: true });
    } else {
        baslat();
        window.msRunPageModules?.();
    }
})();

// Global image lazy load davranisi.
(() => {
    const placeholderSrc = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='32' height='42' viewBox='0 0 32 42'%3E%3Crect width='32' height='42' fill='%23f1f5f9'/%3E%3C/svg%3E";
    const lazyInfiniteSecici = ".lazy-infinite-on";
    let lazyObserver = null;

    const lazyInfiniteAktifMi = (oge) => Boolean(oge?.closest?.(lazyInfiniteSecici));

    const lazyKapsamlariBul = (kok = document) => {
        if (kok instanceof HTMLImageElement) {
            return lazyInfiniteAktifMi(kok) ? [kok] : [];
        }

        if (kok instanceof Element && lazyInfiniteAktifMi(kok)) {
            return [kok];
        }

        if (!kok?.querySelectorAll) {
            return [];
        }

        return Array.from(kok.querySelectorAll(lazyInfiniteSecici));
    };

    const skeletonHazirla = (img) => {
        if (img.dataset.msLazySkeleton !== "true" || img.closest(".ms-lazy-placeholderli")) {
            return;
        }

        if (!img.parentNode) {
            return;
        }

        const kapsayici = document.createElement("span");
        kapsayici.className = "ms-lazy-placeholderli";
        img.parentNode.insertBefore(kapsayici, img);
        kapsayici.appendChild(img);

        const placeholder = document.createElement("span");
        placeholder.className = "ms-lazy-placeholder-zemin";
        placeholder.setAttribute("aria-hidden", "true");
        const urunGorseli = img.dataset.msUrunGorselYukleme === "true";
        if (urunGorseli) {
            kapsayici.classList.add("ms-urun-gorsel-placeholderli");
            placeholder.classList.add("ms-urun-markali-placeholder");
        }
        placeholder.innerHTML = urunGorseli ? "<span>Tozlu</span>" : "<span>Placeholder</span>";

        const skeleton = document.createElement("span");
        skeleton.className = "ms-lazy-skeleton";
        skeleton.setAttribute("aria-hidden", "true");

        kapsayici.appendChild(placeholder);
        if (!urunGorseli) {
            kapsayici.appendChild(skeleton);
        }
    };

    const urunGorseliniHazirla = (img, sifirla = false) => {
        if (!(img instanceof HTMLImageElement) || img.dataset.msUrunGorselYukleme !== "true") {
            return;
        }

        if (sifirla) {
            const nesil = Number.parseInt(img.dataset.msUrunGorselNesil || "0", 10) + 1;
            img.dataset.msUrunGorselNesil = String(nesil);
            // Markalı placeholder yalnız kartın ilk görseli hazırlanırken görünür.
            // Hover/renk değişimlerinde mevcut görsel yeni kaynak yüklenene kadar korunur.
            if (img.dataset.msUrunGorselIlkYuklendi !== "true") {
                img.classList.remove("ms-lazy-gorsel-yuklendi");
            }
        }

        const tamamla = async () => {
            // Lazy placeholder'ın kendi data URI load olayı gerçek ürün görseli değildir.
            if (img.dataset.msLazySrc || img.dataset.msLazySrcset || !img.complete || img.naturalWidth <= 0) {
                return;
            }

            const nesil = img.dataset.msUrunGorselNesil || "0";
            try {
                await img.decode();
            } catch {
                // İlk yükleme başarısızsa placeholder kalır; sonraki galeri geçişleri
                // daha önce başarıyla yüklenmiş görseli placeholder ile örtmez.
                if ((img.dataset.msUrunGorselNesil || "0") === nesil
                    && img.dataset.msUrunGorselIlkYuklendi !== "true") {
                    img.classList.remove("ms-lazy-gorsel-yuklendi");
                }
                return;
            }

            if ((img.dataset.msUrunGorselNesil || "0") === nesil && img.complete && img.naturalWidth > 0) {
                img.dataset.msUrunGorselIlkYuklendi = "true";
                img.classList.add("ms-lazy-gorsel-yuklendi");
            }
        };

        if (img.dataset.msUrunGorselHazir !== "true") {
            img.dataset.msUrunGorselHazir = "true";
            img.addEventListener("load", tamamla);
            img.addEventListener("error", () => {
                if (img.dataset.msUrunGorselIlkYuklendi !== "true") {
                    img.classList.remove("ms-lazy-gorsel-yuklendi");
                }
            });
        }

        if (!img.dataset.msLazySrc && !img.dataset.msLazySrcset && img.complete) {
            void tamamla();
        }
    };

    const urunGorselleriniHazirla = (kok = document) => {
        if (kok instanceof HTMLImageElement) {
            urunGorseliniHazirla(kok);
            return;
        }
        kok?.querySelectorAll?.("img[data-ms-urun-gorsel-yukleme='true']")
            .forEach((img) => urunGorseliniHazirla(img));
    };

    window.msUrunGorselYuklemeyeHazirla = urunGorseliniHazirla;

    const gorselYukle = (img) => {
        const lazySrc = img.dataset.msLazySrc;
        const lazySrcset = img.dataset.msLazySrcset;
        const lazySizes = img.dataset.msLazySizes;
        const lazyPictureSources = img.closest("picture")?.querySelectorAll("source[data-ms-lazy-srcset]") || [];

        if (!lazySrc && !lazySrcset && lazyPictureSources.length === 0) {
            if (img.dataset.msUrunGorselYukleme === "true") {
                urunGorseliniHazirla(img);
            } else {
                img.classList.add("ms-lazy-gorsel-yuklendi");
            }
            return;
        }

        if (img.dataset.msUrunGorselYukleme === "true") {
            urunGorseliniHazirla(img, true);
        } else {
            img.addEventListener("load", () => {
                img.classList.add("ms-lazy-gorsel-yuklendi");
            }, { once: true });
        }

        if (lazySizes) {
            img.sizes = lazySizes;
        }

        if (lazySrcset) {
            img.srcset = lazySrcset;
        }

        lazyPictureSources.forEach((source) => {
            source.srcset = source.dataset.msLazySrcset;

            if (source.dataset.msLazySizes) {
                source.sizes = source.dataset.msLazySizes;
            }

            source.removeAttribute("data-ms-lazy-srcset");
            source.removeAttribute("data-ms-lazy-sizes");
        });

        if (lazySrc) {
            img.src = lazySrc;
        }

        img.removeAttribute("data-ms-lazy-src");
        img.removeAttribute("data-ms-lazy-srcset");
        img.removeAttribute("data-ms-lazy-sizes");
        urunGorseliniHazirla(img);
    };

    const gorselHazirla = (img) => {
        urunGorseliniHazirla(img);
        if (!(img instanceof HTMLImageElement) || !lazyInfiniteAktifMi(img) || img.dataset.msLazyHazir === "true" || img.dataset.msLazy === "false" || img.classList.contains("no-lazy")) {
            return;
        }

        if (!img.hasAttribute("loading")) {
            img.loading = "lazy";
        }

        if (!img.hasAttribute("decoding")) {
            img.decoding = "async";
        }

        // Kaynagi henuz atanmamis (infinite-scroll iskelet) gorsel "hazir" sayilmaz;
        // kartDoldur data-ms-lazy-src yazinca sonraki yenile cagrisi burayi yeniden isler.
        if (!img.dataset.msLazySrc && !img.dataset.msLazySrcset) {
            return;
        }

        img.dataset.msLazyHazir = "true";

        img.classList.add("ms-lazy-gorsel");
        skeletonHazirla(img);

        if (!img.getAttribute("src")) {
            img.src = placeholderSrc;
        }

        if (lazyObserver) {
            lazyObserver.observe(img);
        } else {
            gorselYukle(img);
        }
    };

    const lazyLoadYenile = (kok = document) => {
        lazyKapsamlariBul(kok).forEach((kapsam) => {
            if (kapsam instanceof HTMLImageElement) {
                gorselHazirla(kapsam);
                return;
            }

            kapsam.querySelectorAll("img").forEach(gorselHazirla);
        });
    };

    if ("IntersectionObserver" in window) {
        lazyObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) {
                    return;
                }

                observer.unobserve(entry.target);
                gorselYukle(entry.target);
            });
        }, {
            rootMargin: "240px 0px",
            threshold: 0.01
        });
    }

    const baslat = () => {
        urunGorselleriniHazirla();
        lazyLoadYenile();

        if ("MutationObserver" in window) {
            const mutationObserver = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        urunGorselleriniHazirla(node);
                        if (node instanceof HTMLImageElement) {
                            gorselHazirla(node);
                        } else {
                            lazyLoadYenile(node);
                        }
                    });
                });
            });

            mutationObserver.observe(document.documentElement, {
                childList: true,
                subtree: true
            });
        }
    };

    const lazyInfiniteYenile = (kok = document) => {
        lazyLoadYenile(kok);
        window.msInfiniteScrollBaslat?.(kok);
        window.msInfiniteOrnekleriBaslat?.(kok);
    };

    window.msLazyLoadYenile = lazyInfiniteYenile;
    window.msLazyInfiniteYenile = lazyInfiniteYenile;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", baslat, { once: true });
    } else {
        baslat();
    }
})();

// Kurumsal SSS akordiyonunda ayni anda tek soru acik kalir.
(() => {
    const baslat = (kok = document) => {
        if (!kok?.querySelectorAll) {
            return;
        }

        kok.querySelectorAll(".ms-kurumsal-sss").forEach((sssAlani) => {
            if (sssAlani.dataset.msKurumsalSssHazir === "true") {
                return;
            }

            sssAlani.dataset.msKurumsalSssHazir = "true";
            const detaylar = Array.from(sssAlani.querySelectorAll("details"));

            detaylar.forEach((detay) => {
                detay.addEventListener("toggle", () => {
                    if (!detay.open) {
                        return;
                    }

                    detaylar.forEach((digerDetay) => {
                        if (digerDetay !== detay) {
                            digerDetay.open = false;
                        }
                    });
                });
            });
        });
    };

    window.msKurumsalSssAkordiyonBaslat = baslat;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => baslat(), { once: true });
    } else {
        baslat();
    }
})();

// ProjeElementleri ana sayfa sekme, filtre, select, modal ve form element davranislari.
(() => {
            const projeKok = document.querySelector("[data-ms-proje-elementleri]");
            const projeKapsam = projeKok || document;
            const sekmeler = Array.from(projeKapsam.querySelectorAll(".ms-sekme"));
            let paneller = Array.from(projeKapsam.querySelectorAll(".ms-panel-alani > .ms-panel"));
            const panelAlani = projeKapsam.querySelector(".ms-panel-alani");
            const anaYerlesim = projeKapsam.querySelector(".ms-ana-yerlesim");
            const mobilOnizleme = projeKapsam.querySelector("[data-ms-proje-mobil-onizleme]");
            const mobilIframe = projeKapsam.querySelector("[data-ms-proje-mobil-iframe]");
            const viewportButonlari = Array.from(projeKapsam.querySelectorAll("[data-ms-proje-viewport]"));
            const butonBoyutlari = document.querySelectorAll(".ms-buton-boyut");
            const ornekButonlar = document.querySelectorAll(".ms-buton-ornek");
            const kodGirisleri = document.querySelectorAll("[data-ms-kod-giris]");
            const kodDetaylari = document.querySelectorAll("[data-code-detail]");
            const ornekModalAcButonlari = document.querySelectorAll("[data-ms-ornek-modal-ac]");
            const ornekModallar = document.querySelectorAll("[data-ms-ornek-modal]");
            const ornekModalBoyutClasslari = ["ms-ornek-modal-boyut-m", "ms-ornek-modal-boyut-l", "ms-ornek-modal-boyut-xl", "ms-ornek-modal-boyut-2xl"];
            const boyutClasslari = ["ms-buton-x", "ms-buton-s", "ms-buton-m", "ms-buton-l", "ms-buton-xl", "ms-buton-xxl"];
            const varsayilanProjeSekmesi = projeKok?.dataset.msProjeVarsayilanSekme || projeKapsam.querySelector(".ms-sekme-aktif")?.dataset.tab || "gorunum-tipleri";
            const arayuzTablari = new Set(["butonlar", "filtreler", "rozetler", "ikons", "bildirimler", "mobil-menu-kaydirma", "modallar"]);
            const panelYuklemeIstekleri = new WeakMap();

            let sonOdaklananEleman = null;
            const projeViewportAyarla = (mod) => {
                const mobil = mod === "mobil";

                document.body.classList.toggle("ms-proje-mobil-onizleme-acik", mobil);
                anaYerlesim?.toggleAttribute("hidden", mobil);

                if (mobilOnizleme) {
                    mobilOnizleme.hidden = !mobil;
                }

                if (mobil && mobilIframe) {
                    const iframeUrl = new URL(window.location.href);
                    iframeUrl.searchParams.set("ms_cerceve", "1");

                    if (mobilIframe.getAttribute("src") !== iframeUrl.toString()) {
                        mobilIframe.src = iframeUrl.toString();
                    }
                }

                viewportButonlari.forEach((buton) => {
                    const aktif = buton.dataset.msProjeViewport === mod;
                    buton.classList.toggle("ms-proje-viewport-buton-aktif", aktif);
                    buton.setAttribute("aria-pressed", aktif.toString());
                });
            };

            viewportButonlari.forEach((buton) => {
                buton.addEventListener("click", () => {
                    projeViewportAyarla(buton.dataset.msProjeViewport || "desktop");
                });
            });

            projeViewportAyarla("desktop");

            const ornekModalKapat = () => {
                document.querySelectorAll("[data-ms-ornek-modal]").forEach((modal) => {
                    modal.classList.remove("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "true");
                });
                document.body.style.overflow = "";
                sonOdaklananEleman?.focus?.();
            };

            const ornekModalAc = (modalTuru, modalBoyutu = "m") => {
                const modal = document.querySelector(`[data-ms-ornek-modal="${modalTuru}"]`);

                if (!modal) {
                    return;
                }

                const modalKutusu = modal.querySelector(".ms-ornek-modal-kutu");
                modalKutusu?.classList.remove(...ornekModalBoyutClasslari);
                modalKutusu?.classList.add(`ms-ornek-modal-boyut-${modalBoyutu}`);

                sonOdaklananEleman = document.activeElement;
                ornekModalKapat();
                modal.classList.add("ms-ornek-modal-acik");
                modal.setAttribute("aria-hidden", "false");
                document.body.style.overflow = "hidden";

                window.setTimeout(() => {
                    modal.querySelector("button, a")?.focus();
                }, 40);
            };

            const ornekModallariBaslat = (kok = document) => {
                alanlariSec(kok, "[data-ms-ornek-modal-ac]").forEach((buton) => {
                    if (buton.dataset.msOrnekModalHazir === "true") {
                        return;
                    }

                    buton.dataset.msOrnekModalHazir = "true";
                    buton.addEventListener("click", () => {
                        ornekModalAc(buton.dataset.msOrnekModalAc, buton.dataset.msOrnekModalBoyut || "m");
                    });
                });

                alanlariSec(kok, "[data-ms-ornek-modal]").forEach((modal) => {
                    modal.querySelectorAll("[data-ms-ornek-modal-kapat]").forEach((kapatici) => {
                        if (kapatici.dataset.msOrnekModalKapatHazir === "true") {
                            return;
                        }

                        kapatici.dataset.msOrnekModalKapatHazir = "true";
                        kapatici.addEventListener("click", ornekModalKapat);
                    });
                });
            };

            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    ornekModalKapat();
                }
            });

            const projeElementleriTabGoster = (aktifSekme, urlGuncelle = true) => {
                const hedefSekme = sekmeler.find((sekme) => sekme.dataset.tab === aktifSekme)
                    || sekmeler.find((sekme) => sekme.dataset.tab === varsayilanProjeSekmesi)
                    || sekmeler[0];
                const hedefPanel = hedefSekme?.dataset.tab;

                if (!hedefPanel) {
                    return;
                }

                let mevcutPanel = paneller.find((panel) => panel.dataset.panel === hedefPanel);

                if (!mevcutPanel && panelAlani) {
                    mevcutPanel = document.createElement("article");
                    mevcutPanel.className = "ms-panel";
                    mevcutPanel.dataset.panel = hedefPanel;
                    mevcutPanel.dataset.msLazyPanelUrl = `/ProjeElementleri/Panel?panel=${encodeURIComponent(hedefPanel)}`;
                    mevcutPanel.dataset.msLazyPanelYuklendi = "false";
                    panelAlani.appendChild(mevcutPanel);
                    paneller = Array.from(projeKapsam.querySelectorAll(".ms-panel-alani > .ms-panel"));
                }

                sekmeler.forEach((oge) => {
                    const aktif = oge === hedefSekme;
                    oge.setAttribute("aria-pressed", aktif.toString());
                    oge.classList.toggle("ms-sekme-aktif", aktif);
                });

                paneller.forEach((panel) => {
                    const aktif = panel.dataset.panel === hedefPanel;
                    panel.classList.toggle("ms-gizli", !aktif);
                    panel.hidden = !aktif;
                    panel.setAttribute("aria-hidden", (!aktif).toString());
                });

                panelIcerikYukle(mevcutPanel).then(() => {
                    if (mevcutPanel?.dataset.msLazyPanelYuklendi !== "true") {
                        return;
                    }

                    window.msRunPageModules?.(mevcutPanel || document);
                    projeElementleriKapsamBaslat(mevcutPanel || document);
                    window.msLazyLoadYenile?.(mevcutPanel || document);
                });

                if (urlGuncelle) {
                    const url = new URL(window.location.href);
                    url.searchParams.set("utm", hedefPanel);
                    window.history.replaceState({}, "", url);
                }
            };

            const urlParametreGuncelle = (anahtar, deger) => {
                const url = new URL(window.location.href);
                url.searchParams.set(anahtar, deger);
                window.history.replaceState({}, "", url);
            };

            const alanlariSec = (kok, secici) => {
                if (!kok?.querySelectorAll) {
                    return [];
                }

                const alanlar = [];

                if (kok.matches?.(secici)) {
                    alanlar.push(kok);
                }

                kok.querySelectorAll(secici).forEach((alan) => alanlar.push(alan));
                return Array.from(new Set(alanlar));
            };

            const metniKopyala = async (metin) => {
                if (navigator.clipboard?.writeText) {
                    await navigator.clipboard.writeText(metin);
                    return;
                }

                const alan = document.createElement("textarea");
                alan.value = metin;
                alan.setAttribute("readonly", "readonly");
                alan.style.position = "fixed";
                alan.style.inset = "0 auto auto 0";
                alan.style.opacity = "0";
                document.body.appendChild(alan);
                alan.select();
                document.execCommand("copy");
                alan.remove();
            };

            const ikonKatalogBaslat = (kok = document) => {
                alanlariSec(kok, "[data-panel='ikons'], [data-ms-arayuz-panel='ikons']").forEach((ikonPaneli) => {
                    if (ikonPaneli.dataset.msIkonKatalogHazir === "true") {
                        return;
                    }

                    ikonPaneli.dataset.msIkonKatalogHazir = "true";

                    const ikonArama = ikonPaneli.querySelector("[data-ms-ikon-arama]");
                    const ikonKartlari = Array.from(ikonPaneli.querySelectorAll("[data-ms-ikon-kart]"));

                    ikonArama?.addEventListener("input", () => {
                        const aranan = ikonArama.value.toLocaleLowerCase("tr-TR").trim();

                        ikonKartlari.forEach((kart) => {
                            const ikonAdi = kart.getAttribute("data-ms-ikon-ad") || "";
                            kart.hidden = aranan.length > 0 && !ikonAdi.includes(aranan);
                        });
                    });

                    ikonPaneli.querySelectorAll("[data-ms-ikon-kopyala]").forEach((buton) => {
                        buton.addEventListener("click", async () => {
                            const html = buton.getAttribute("data-ms-ikon-kopyala") || "";
                            const durum = buton.querySelector("[data-ms-ikon-kopya-durum]");

                            try {
                                await metniKopyala(html);
                                durum.textContent = "Kopyalandı";
                                buton.classList.add("ms-ikon-kopyalandi");
                                window.setTimeout(() => {
                                    durum.textContent = "";
                                    buton.classList.remove("ms-ikon-kopyalandi");
                                }, 1200);
                            } catch {
                                durum.textContent = "Kopyalanamadı";
                            }
                        });
                    });
                });
            };

            const butonOrnekleriBaslat = (kok = document) => {
                alanlariSec(kok, ".ms-buton-boyut-secici").forEach((secici) => {
                    if (secici.dataset.msButonBoyutHazir === "true") {
                        return;
                    }

                    secici.dataset.msButonBoyutHazir = "true";

                    const panel = secici.closest("[data-ms-arayuz-panel], [data-panel]") || document;
                    const seciciButonlari = Array.from(secici.querySelectorAll(".ms-buton-boyut"));
                    const panelOrnekButonlari = Array.from(panel.querySelectorAll(".ms-buton-ornek"));

                    seciciButonlari.forEach((buton) => {
                        buton.addEventListener("click", () => {
                            const secilenBoyut = buton.dataset.buttonSize;

                            seciciButonlari.forEach((oge) => {
                                const aktif = oge === buton;
                                oge.setAttribute("aria-pressed", aktif.toString());
                                oge.classList.toggle("ms-buton-boyut-aktif", aktif);
                            });

                            panelOrnekButonlari.forEach((ornek) => {
                                ornek.classList.remove(...boyutClasslari);
                                ornek.classList.add(secilenBoyut);
                            });
                        });
                    });
                });
            };

            const tabGrubuGoster = (sekmeler, paneller, aktifDeger, sekmeDataAdi, panelDataAdi, aktifClass = "ms-kod-sekme-aktif", urlAnahtari = "", urlGuncelle = true) => {
                const hedefSekme = sekmeler.find((sekme) => sekme.dataset[sekmeDataAdi] === aktifDeger) || sekmeler[0];
                const hedefPanel = hedefSekme?.dataset[sekmeDataAdi];

                if (!hedefPanel) {
                    return;
                }

                sekmeler.forEach((oge) => {
                    const aktif = oge === hedefSekme;
                    oge.classList.toggle(aktifClass, aktif);
                    oge.setAttribute("aria-pressed", aktif.toString());
                });

                paneller.forEach((panel) => {
                    const aktif = panel.dataset[panelDataAdi] === hedefPanel;
                    panel.classList.toggle("ms-gizli", !aktif);

                    if (panel.hasAttribute("hidden")) {
                        panel.hidden = !aktif;
                    }

                    if (aktif) {
                        panelIcerikYukle(panel).then(() => {
                            projeElementleriKapsamBaslat(panel);
                            window.msRunPageModules?.(panel);
                            window.msFiltreBloklariBaslat?.(panel);
                            window.msSiralamaSelectleriBaslat?.(panel);
                            window.msLazyLoadYenile?.(panel);
                            window.msUrunKartDavranislariYenile?.(panel);
                        });
                    }
                });

                if (urlAnahtari && urlGuncelle) {
                    urlParametreGuncelle(urlAnahtari, hedefPanel);
                }
            };

            const scriptleriCalistir = (kok) => {
                kok.querySelectorAll("script").forEach((script) => {
                    const yeniScript = document.createElement("script");

                    Array.from(script.attributes).forEach((attr) => {
                        yeniScript.setAttribute(attr.name, attr.value);
                    });

                    yeniScript.textContent = script.textContent;
                    script.replaceWith(yeniScript);
                });
            };

            const panelIcerikYukle = (panel) => {
                if (!panel) {
                    return Promise.resolve();
                }

                if (panel.dataset.msLazyPanelYuklendi === "true") {
                    return Promise.resolve(panel);
                }

                const devamEdenIstek = panelYuklemeIstekleri.get(panel);

                if (devamEdenIstek) {
                    return devamEdenIstek;
                }

                const yuklemeIstegi = (async () => {
                    const url = panel.dataset.msLazyPanelUrl;

                    if (!url) {
                        panel.dataset.msLazyPanelYuklendi = "true";
                        return panel;
                    }

                    panel.dataset.msLazyPanelYukleniyor = "true";
                    panel.setAttribute("aria-busy", "true");
                    panel.classList.add("ms-panel-yukleniyor");

                    if (!panel.innerHTML.trim()) {
                        panel.innerHTML = `<div class="ms-panel-yukleniyor-durum" role="status">Icerik yukleniyor...</div>`;
                    }

                    try {
                        const response = await fetch(url, {
                            headers: {
                                "X-Requested-With": "XMLHttpRequest"
                            }
                        });

                        if (!response.ok) {
                            throw new Error(`Panel yuklenemedi: ${response.status}`);
                        }

                        const html = await response.text();
                        const taslak = document.createElement("template");
                        taslak.innerHTML = html.trim();
                        const gelenPanel = taslak.content.firstElementChild;

                        if (panel.dataset.panel && gelenPanel?.dataset?.panel === panel.dataset.panel) {
                            const panelKodu = panel.dataset.panel;
                            const lazyPanelUrl = panel.dataset.msLazyPanelUrl;
                            const panelGizli = panel.hidden || panel.classList.contains("ms-gizli") || panel.getAttribute("aria-hidden") === "true";

                            Array.from(panel.attributes).forEach((attr) => panel.removeAttribute(attr.name));
                            Array.from(gelenPanel.attributes).forEach((attr) => {
                                panel.setAttribute(attr.name, attr.value);
                            });

                            panel.dataset.panel = panel.dataset.panel || panelKodu;

                            if (lazyPanelUrl) {
                                panel.dataset.msLazyPanelUrl = lazyPanelUrl;
                            }

                            panel.innerHTML = gelenPanel.innerHTML;
                            panel.classList.toggle("ms-gizli", panelGizli);
                            panel.hidden = panelGizli;
                            panel.setAttribute("aria-hidden", panelGizli.toString());
                        } else {
                            panel.innerHTML = html;
                        }

                        panel.dataset.msLazyPanelYuklendi = "true";
                        delete panel.dataset.msSayfalarHazir;
                        delete panel.dataset.msHesabimSekmeleriHazir;
                        delete panel.dataset.msKurumsalSekmeleriHazir;
                        delete panel.dataset.msGorunumTipleriHazir;
                        scriptleriCalistir(panel);
                        projeElementleriKapsamBaslat(panel);
                        window.msRunPageModules?.(panel);
                        window.msFiltreBloklariBaslat?.(panel);
                        window.msSiralamaSelectleriBaslat?.(panel);
                        window.msLazyLoadYenile?.(panel);
                        window.msUrunKartDavranislariYenile?.(panel);
                        window.msProjeElementleriScrollGeriYukle?.();
                    } catch (error) {
                        panel.innerHTML = `<div class="ms-uyari ms-uyari-hata"><strong>Icerik yuklenemedi</strong><span>${error.message}</span></div>`;
                    } finally {
                        panel.dataset.msLazyPanelYukleniyor = "false";
                        panel.setAttribute("aria-busy", "false");
                        panel.classList.remove("ms-panel-yukleniyor");
                        panelYuklemeIstekleri.delete(panel);
                    }

                    return panel;
                })();

                panelYuklemeIstekleri.set(panel, yuklemeIstegi);
                return yuklemeIstegi;
            };

            const gorunumTipiOrnekleriGuncelle = (kok = document) => {
                alanlariSec(kok, "[data-ms-gorunum-carousel]").forEach((carousel) => {
                    carousel.msGorunumCarouselGuncelle?.();
                });
            };

            const gorunumTipiOrnekleriBaslat = (kok = document) => {
                alanlariSec(kok, "[data-ms-gorunum-carousel]").forEach((carousel) => {
                    if (carousel.dataset.msGorunumCarouselHazir === "true") {
                        return;
                    }

                    carousel.dataset.msGorunumCarouselHazir = "true";

                    const liste = carousel.querySelector("[data-ms-gorunum-carousel-liste]");
                    const solKontrol = carousel.querySelector("[data-ms-gorunum-carousel-kontrol='sol']");
                    const sagKontrol = carousel.querySelector("[data-ms-gorunum-carousel-kontrol='sag']");
                    const sayac = carousel.querySelector("[data-ms-gorunum-carousel-sayac]");
                    const noktalar = Array.from(carousel.querySelectorAll("[data-ms-gorunum-carousel-nokta]"));
                    const serbestKaydirma = carousel.hasAttribute("data-ms-gorunum-carousel-serbest");
                    const cercevesizCarousel = carousel.classList.contains("ms-gorunum-carousel-demo-cercevesiz");
                    let surukleniyor = false;
                    let suruklemeYapildi = false;
                    let baslangicX = 0;
                    let baslangicY = 0;
                    let baslangicScroll = 0;
                    let suruklemeYonu = null;
                    let tiklamaEngellenecek = false;
                    let suruklemeAnimasyonKaresi = 0;
                    let guncellemeAnimasyonKaresi = 0;
                    let hedefScroll = 0;

                    if (!liste) {
                        return;
                    }

                    const kartlariAl = () => Array.from(liste.children);
                    const enYuksekScrolluAl = () => Math.max(0, liste.scrollWidth - liste.clientWidth);
                    const scrolluSinirla = (deger) => Math.min(enYuksekScrolluAl(), Math.max(0, deger));
                    const kartScrollSolunuAl = (kart) => scrolluSinirla(kart.offsetLeft - liste.offsetLeft);

                    const aktifKartIndexiniBul = () => {
                        const kartlar = kartlariAl();

                        if (!kartlar.length) {
                            return 0;
                        }

                        let aktifIndex = 0;
                        let enYakinMesafe = Number.POSITIVE_INFINITY;

                        kartlar.forEach((kart, index) => {
                            const mesafe = Math.abs(kartScrollSolunuAl(kart) - liste.scrollLeft);

                            if (mesafe < enYakinMesafe) {
                                aktifIndex = index;
                                enYakinMesafe = mesafe;
                            }
                        });

                        return aktifIndex;
                    };

                    const kartaGit = (index, behavior = "smooth") => {
                        const kartlar = kartlariAl();

                        if (!kartlar.length) {
                            return;
                        }

                        const hedefIndex = Math.min(kartlar.length - 1, Math.max(0, index));

                        liste.scrollTo({
                            left: kartScrollSolunuAl(kartlar[hedefIndex]),
                            behavior
                        });
                    };

                    const suruklemeScrollunuAyarla = (deger) => {
                        if (!cercevesizCarousel) {
                            liste.scrollLeft = deger;
                            guncellePlanla();
                            return;
                        }

                        hedefScroll = deger;

                        if (suruklemeAnimasyonKaresi) {
                            return;
                        }

                        suruklemeAnimasyonKaresi = window.requestAnimationFrame(() => {
                            liste.scrollLeft = hedefScroll;
                            suruklemeAnimasyonKaresi = 0;
                            guncellePlanla();
                        });
                    };

                    const guncelle = () => {
                        const kartlar = kartlariAl();
                        const kaydirilabilir = liste.scrollWidth > liste.clientWidth + 2;
                        const basta = liste.scrollLeft <= 1;
                        const sonda = liste.scrollLeft + liste.clientWidth >= liste.scrollWidth - 1;

                        solKontrol?.toggleAttribute("disabled", !kaydirilabilir || basta);
                        sagKontrol?.toggleAttribute("disabled", !kaydirilabilir || sonda);

                        if (kartlar.length > 0) {
                            const aktifIndex = aktifKartIndexiniBul();

                            if (sayac) {
                                sayac.textContent = `${aktifIndex + 1} / ${kartlar.length}`;
                            }

                            noktalar.forEach((nokta, noktaIndex) => {
                                const aktif = noktaIndex === aktifIndex;
                                nokta.classList.toggle("ms-gorunum-carousel-cercevesiz-nokta-aktif", aktif);
                                nokta.setAttribute("aria-pressed", aktif.toString());
                            });
                        }
                    };

                    const guncellePlanla = () => {
                        if (guncellemeAnimasyonKaresi) {
                            return;
                        }

                        guncellemeAnimasyonKaresi = window.requestAnimationFrame(() => {
                            guncellemeAnimasyonKaresi = 0;
                            guncelle();
                        });
                    };

                    carousel.msGorunumCarouselGuncelle = guncellePlanla;

                    const kaydir = (yon) => {
                        const yonCarpani = yon === "sag" ? 1 : -1;
                        kartaGit(aktifKartIndexiniBul() + yonCarpani);
                    };

                    solKontrol?.addEventListener("click", () => kaydir("sol"));
                    sagKontrol?.addEventListener("click", () => kaydir("sag"));
                    noktalar.forEach((nokta) => {
                        nokta.addEventListener("click", () => {
                            kartaGit(Number(nokta.dataset.msGorunumCarouselNokta || 0));
                        });
                    });
                    liste.addEventListener("scroll", guncellePlanla, { passive: true });
                    liste.addEventListener("dragstart", (event) => event.preventDefault());
                    liste.addEventListener("click", (event) => {
                        if (tiklamaEngellenecek) {
                            event.preventDefault();
                            tiklamaEngellenecek = false;
                        }
                    });
                    const kartIciEtkilesimliHedefMi = (hedef) => hedef instanceof Element
                        && Boolean(hedef.closest("button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], [data-ms-urun-video], .ms-urun-video-alani, .ms-urun-renk-tooltip-alani, .ms-urun-renk-rozet, .ms-urun-slider-noktalari"));

                    liste.addEventListener("pointerdown", (event) => {
                        if (kartIciEtkilesimliHedefMi(event.target)) {
                            return;
                        }

                        if (event.button !== undefined && event.button !== 0) {
                            return;
                        }

                        surukleniyor = true;
                        suruklemeYapildi = false;
                        tiklamaEngellenecek = false;
                        baslangicX = event.clientX;
                        baslangicY = event.clientY;
                        baslangicScroll = liste.scrollLeft;
                        suruklemeYonu = null;
                    });
                    liste.addEventListener("pointermove", (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        const fark = event.clientX - baslangicX;
                        const dikeyFark = event.clientY - baslangicY;

                        if (!suruklemeYonu && (Math.abs(fark) > 6 || Math.abs(dikeyFark) > 6)) {
                            suruklemeYonu = Math.abs(fark) > Math.abs(dikeyFark) ? "yatay" : "dikey";

                            if (suruklemeYonu === "yatay") {
                                liste.classList.add("ms-gorunum-carousel-surukleniyor");
                                liste.setPointerCapture?.(event.pointerId);
                            } else {
                                surukleniyor = false;
                                liste.classList.remove("ms-gorunum-carousel-surukleniyor");
                                return;
                            }
                        }

                        if (suruklemeYonu === "yatay" && Math.abs(fark) > 6) {
                            suruklemeYapildi = true;
                            tiklamaEngellenecek = true;
                            event.preventDefault();
                        }

                        if (suruklemeYonu !== "yatay") {
                            return;
                        }

                        suruklemeScrollunuAyarla(baslangicScroll - fark);
                    });

                    const suruklemeyiBitir = (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        if (cercevesizCarousel && suruklemeAnimasyonKaresi) {
                            window.cancelAnimationFrame(suruklemeAnimasyonKaresi);
                            suruklemeAnimasyonKaresi = 0;
                            liste.scrollLeft = hedefScroll;
                        }

                        const hizalanacakIndex = suruklemeYapildi && !serbestKaydirma ? aktifKartIndexiniBul() : -1;
                        surukleniyor = false;
                        suruklemeYonu = null;
                        liste.classList.remove("ms-gorunum-carousel-surukleniyor");

                        if (typeof event.pointerId === "number" && liste.hasPointerCapture?.(event.pointerId)) {
                            liste.releasePointerCapture(event.pointerId);
                        }

                        if (hizalanacakIndex >= 0) {
                            kartaGit(hizalanacakIndex);
                        } else {
                            guncellePlanla();
                        }
                    };

                    liste.addEventListener("pointerup", suruklemeyiBitir);
                    liste.addEventListener("pointercancel", suruklemeyiBitir);
                    liste.addEventListener("mouseleave", suruklemeyiBitir);
                    if ("ResizeObserver" in window) {
                        const carouselBoyutGozlemcisi = new ResizeObserver(guncellePlanla);
                        carouselBoyutGozlemcisi.observe(liste);
                    } else {
                        window.addEventListener("resize", guncellePlanla);
                    }
                    guncellePlanla();
                });

                alanlariSec(kok, "[data-ms-gorunum-icerik-tabs]").forEach((tabAlani) => {
                    if (tabAlani.dataset.msGorunumIcerikTabsHazir === "true") {
                        return;
                    }

                    tabAlani.dataset.msGorunumIcerikTabsHazir = "true";

                    const sekmeler = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-icerik-tab]"));
                    const paneller = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-icerik-panel]"));

                    const panelGoster = (hedef) => {
                        sekmeler.forEach((sekme) => {
                            const aktif = sekme.dataset.msGorunumIcerikTab === hedef;
                            sekme.classList.toggle("ms-gorunum-mini-tab-aktif", aktif);
                            sekme.setAttribute("aria-pressed", aktif.toString());
                        });

                        paneller.forEach((panel) => {
                            panel.classList.toggle("ms-gizli", panel.dataset.msGorunumIcerikPanel !== hedef);
                        });
                    };

                    panelGoster(sekmeler[0]?.dataset.msGorunumIcerikTab);
                    sekmeler.forEach((sekme) => {
                        sekme.addEventListener("click", () => panelGoster(sekme.dataset.msGorunumIcerikTab));
                    });
                });

                // Carousel vitrin türlerini aynı alandaki mini sekmelerle değiştirir.
                alanlariSec(kok, "[data-ms-gorunum-carousel-tabs]").forEach((tabAlani) => {
                    if (tabAlani.dataset.msGorunumCarouselTabsHazir === "true") {
                        return;
                    }

                    tabAlani.dataset.msGorunumCarouselTabsHazir = "true";

                    const sekmeler = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-carousel-tab]"));
                    const paneller = Array.from(tabAlani.querySelectorAll("[data-ms-gorunum-carousel-panel]"));

                    const panelGoster = (hedef) => {
                        sekmeler.forEach((sekme) => {
                            const aktif = sekme.dataset.msGorunumCarouselTab === hedef;
                            sekme.classList.toggle("ms-gorunum-mini-tab-aktif", aktif);
                            sekme.setAttribute("aria-pressed", aktif.toString());
                        });

                        paneller.forEach((panel) => {
                            panel.classList.toggle("ms-gizli", panel.dataset.msGorunumCarouselPanel !== hedef);
                        });

                        const aktifPanel = paneller.find((panel) => panel.dataset.msGorunumCarouselPanel === hedef);
                        window.msLazyLoadYenile?.(aktifPanel || tabAlani);
                        window.requestAnimationFrame(() => {
                            aktifPanel?.querySelectorAll("[data-ms-gorunum-carousel]").forEach((carousel) => {
                                carousel.msGorunumCarouselGuncelle?.();
                            });
                        });
                    };

                    panelGoster(sekmeler[0]?.dataset.msGorunumCarouselTab);
                    sekmeler.forEach((sekme) => {
                        sekme.addEventListener("click", () => panelGoster(sekme.dataset.msGorunumCarouselTab));
                    });
                });

                alanlariSec(kok, "[data-ms-gorunum-banner]").forEach((bannerAlani) => {
                    if (bannerAlani.dataset.msGorunumBannerHazir === "true") {
                        return;
                    }

                    bannerAlani.dataset.msGorunumBannerHazir = "true";

                    const sekmeler = Array.from(bannerAlani.querySelectorAll("[data-ms-gorunum-banner-tab]"));
                    const paneller = Array.from(bannerAlani.querySelectorAll("[data-ms-gorunum-banner-panel]"));

                    const panelGoster = (hedef) => {
                        sekmeler.forEach((sekme) => {
                            const aktif = sekme.dataset.msGorunumBannerTab === hedef;
                            sekme.classList.toggle("ms-gorunum-mini-tab-aktif", aktif);
                            sekme.setAttribute("aria-pressed", aktif.toString());
                        });

                        paneller.forEach((panel) => {
                            panel.classList.toggle("ms-gizli", panel.dataset.msGorunumBannerPanel !== hedef);
                        });

                        const aktifPanel = paneller.find((panel) => panel.dataset.msGorunumBannerPanel === hedef);
                        window.msLazyLoadYenile?.(aktifPanel || bannerAlani);
                        aktifPanel?.querySelectorAll("[data-ms-gorunum-reklam-vitrin]").forEach((vitrin) => {
                            window.requestAnimationFrame(() => vitrin.msGorunumReklamVitrinGuncelle?.());
                        });
                    };

                    panelGoster(sekmeler[0]?.dataset.msGorunumBannerTab);
                    sekmeler.forEach((sekme) => {
                        sekme.addEventListener("click", () => panelGoster(sekme.dataset.msGorunumBannerTab));
                    });
                });

                alanlariSec(kok, "[data-ms-gorunum-reklam-vitrin]").forEach((vitrin) => {
                    if (vitrin.dataset.msGorunumReklamVitrinHazir === "true") {
                        return;
                    }

                    vitrin.dataset.msGorunumReklamVitrinHazir = "true";

                    const liste = vitrin.querySelector("[data-ms-gorunum-reklam-vitrin-liste]");
                    const solKontrol = vitrin.querySelector("[data-ms-gorunum-reklam-vitrin-kontrol='sol']");
                    const sagKontrol = vitrin.querySelector("[data-ms-gorunum-reklam-vitrin-kontrol='sag']");
                    let surukleniyor = false;
                    let tiklamaEngellenecek = false;
                    let baslangicX = 0;
                    let baslangicScroll = 0;

                    if (!liste || !solKontrol || !sagKontrol) {
                        return;
                    }

                    const kontrolDurumlariniGuncelle = () => {
                        const kaydirilabilir = liste.scrollWidth > liste.clientWidth + 2;
                        const basta = liste.scrollLeft <= 1;
                        const sonda = liste.scrollLeft + liste.clientWidth >= liste.scrollWidth - 1;

                        solKontrol.disabled = !kaydirilabilir || basta;
                        sagKontrol.disabled = !kaydirilabilir || sonda;
                    };

                    vitrin.msGorunumReklamVitrinGuncelle = kontrolDurumlariniGuncelle;

                    const kaydir = (yon) => {
                        const miktar = Math.max(260, Math.floor(liste.clientWidth * 0.72));
                        liste.scrollBy({
                            left: yon === "sag" ? miktar : -miktar,
                            behavior: "smooth"
                        });
                        window.setTimeout(kontrolDurumlariniGuncelle, 260);
                    };

                    solKontrol.addEventListener("click", () => kaydir("sol"));
                    sagKontrol.addEventListener("click", () => kaydir("sag"));
                    liste.addEventListener("scroll", kontrolDurumlariniGuncelle, { passive: true });
                    liste.addEventListener("dragstart", (event) => event.preventDefault());
                    liste.addEventListener("click", (event) => {
                        if (tiklamaEngellenecek) {
                            event.preventDefault();
                            event.stopPropagation();
                            tiklamaEngellenecek = false;
                        }
                    });
                    liste.addEventListener("pointerdown", (event) => {
                        surukleniyor = true;
                        tiklamaEngellenecek = false;
                        baslangicX = event.clientX;
                        baslangicScroll = liste.scrollLeft;
                        liste.classList.add("ms-gorunum-reklam-vitrin-surukleniyor");
                    });
                    liste.addEventListener("pointermove", (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        event.preventDefault();
                        if (Math.abs(event.clientX - baslangicX) > 6) {
                            liste.setPointerCapture?.(event.pointerId);
                            tiklamaEngellenecek = true;
                        }
                        liste.scrollLeft = baslangicScroll - (event.clientX - baslangicX);
                    });

                    const suruklemeyiBitir = (event) => {
                        surukleniyor = false;
                        liste.classList.remove("ms-gorunum-reklam-vitrin-surukleniyor");
                        if (liste.hasPointerCapture?.(event.pointerId)) {
                            liste.releasePointerCapture(event.pointerId);
                        }
                    };

                    liste.addEventListener("pointerup", suruklemeyiBitir);
                    liste.addEventListener("pointercancel", suruklemeyiBitir);
                    liste.addEventListener("mouseleave", suruklemeyiBitir);
                    window.addEventListener("resize", kontrolDurumlariniGuncelle);
                    window.requestAnimationFrame(kontrolDurumlariniGuncelle);
                });
            };

            const gorunumTipleriBaslat = (kok = document) => {
                gorunumTipiOrnekleriBaslat(kok);

                alanlariSec(kok, "[data-ms-gorunum-tipleri]").forEach((gorunumPaneli) => {
                    if (gorunumPaneli.dataset.msGorunumTipleriHazir === "true") {
                        return;
                    }

                    gorunumPaneli.dataset.msGorunumTipleriHazir = "true";

                    const gorunumSekmeleri = Array.from(gorunumPaneli.querySelectorAll("[data-ms-gorunum-tab]"));
                    const gorunumPanelleri = Array.from(gorunumPaneli.querySelectorAll("[data-ms-gorunum-panel]"));
                    const urlGorunumSekmesi = new URLSearchParams(window.location.search).get("utm_gorunum");

                    tabGrubuGoster(gorunumSekmeleri, gorunumPanelleri, urlGorunumSekmesi || "grid", "msGorunumTab", "msGorunumPanel", "ms-kod-sekme-aktif", "utm_gorunum", false);
                    gorunumTipiOrnekleriBaslat(gorunumPaneli);
                    gorunumTipiOrnekleriGuncelle(gorunumPaneli);

                    gorunumSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => {
                            tabGrubuGoster(gorunumSekmeleri, gorunumPanelleri, sekme.dataset.msGorunumTab, "msGorunumTab", "msGorunumPanel", "ms-kod-sekme-aktif", "utm_gorunum");
                            gorunumTipiOrnekleriBaslat(gorunumPaneli);
                            gorunumTipiOrnekleriGuncelle(gorunumPaneli);
                        });
                    });
                });
            };

            const arayuzElementleriBaslat = (kok = document) => {
                alanlariSec(kok, "[data-ms-arayuz-elementleri]").forEach((arayuzPaneli) => {
                    if (arayuzPaneli.dataset.msArayuzElementleriHazir === "true") {
                        return;
                    }

                    arayuzPaneli.dataset.msArayuzElementleriHazir = "true";

                    const arayuzSekmeleri = Array.from(arayuzPaneli.querySelectorAll("[data-ms-arayuz-tab]"));
                    const arayuzPanelleri = Array.from(arayuzPaneli.querySelectorAll("[data-ms-arayuz-panel]"));
                    const url = new URLSearchParams(window.location.search);
                    const eskiAnaSekme = url.get("utm");
                    const urlArayuzSekmesi = url.get("utm_arayuz") || (arayuzTablari.has(eskiAnaSekme) ? eskiAnaSekme : "");

                    tabGrubuGoster(arayuzSekmeleri, arayuzPanelleri, urlArayuzSekmesi || "butonlar", "msArayuzTab", "msArayuzPanel", "ms-kod-sekme-aktif", "utm_arayuz", false);
                    butonOrnekleriBaslat(arayuzPaneli);
                    ornekModallariBaslat(arayuzPaneli);
                    ikonKatalogBaslat(arayuzPaneli);

                    arayuzSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => {
                            tabGrubuGoster(arayuzSekmeleri, arayuzPanelleri, sekme.dataset.msArayuzTab, "msArayuzTab", "msArayuzPanel", "ms-kod-sekme-aktif", "utm_arayuz");
                            butonOrnekleriBaslat(arayuzPaneli);
                            ornekModallariBaslat(arayuzPaneli);
                            ikonKatalogBaslat(arayuzPaneli);
                        });
                    });
                });
            };

            function projeElementleriKapsamBaslat(kok = document) {
                gorunumTipleriBaslat(kok);
                arayuzElementleriBaslat(kok);
                ikonKatalogBaslat(kok);
                butonOrnekleriBaslat(kok);
                ornekModallariBaslat(kok);
                window.msKoleksiyonModallariBaslat?.(kok);
                window.msHesapStatuKartlariBaslat?.(kok);
                window.msTelefonAlanlariniBaslat?.(kok);

                alanlariSec(kok, "[data-panel='sayfalar']").forEach((sayfalarPaneli) => {
                    if (sayfalarPaneli.dataset.msSayfalarHazir === "true") {
                        return;
                    }

                    sayfalarPaneli.dataset.msSayfalarHazir = "true";

                    const sayfaSekmeleri = Array.from(sayfalarPaneli.querySelectorAll("[data-ms-sayfa-tab]"));
                    const sayfaPanelleri = Array.from(sayfalarPaneli.querySelectorAll("[data-ms-sayfa-panel]"));
                    const urlSayfaSekmesi = new URLSearchParams(window.location.search).get("utm_sayfa");

                    tabGrubuGoster(sayfaSekmeleri, sayfaPanelleri, urlSayfaSekmesi || "sepet", "msSayfaTab", "msSayfaPanel", "ms-kod-sekme-aktif", "utm_sayfa", false);

                    sayfaSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => {
                            tabGrubuGoster(sayfaSekmeleri, sayfaPanelleri, sekme.dataset.msSayfaTab, "msSayfaTab", "msSayfaPanel", "ms-kod-sekme-aktif", "utm_sayfa");
                            projeElementleriKapsamBaslat(sayfalarPaneli);
                        });
                    });
                });

                alanlariSec(kok, "[data-ms-sayfa-panel='hesabim']").forEach((hesabimPaneli) => {
                    if (hesabimPaneli.dataset.msHesabimSekmeleriHazir === "true") {
                        return;
                    }

                    hesabimPaneli.dataset.msHesabimSekmeleriHazir = "true";

                    const hesabimSekmeleri = Array.from(hesabimPaneli.querySelectorAll("[data-ms-hesabim-tab]"));
                    const hesabimPanelleri = Array.from(hesabimPaneli.querySelectorAll("[data-ms-hesabim-panel]"));
                    const urlHesabimSekmesi = new URLSearchParams(window.location.search).get("utm_hesabim");

                    tabGrubuGoster(hesabimSekmeleri, hesabimPanelleri, urlHesabimSekmesi || "hesabim-varsayilan", "msHesabimTab", "msHesabimPanel", "ms-kod-sekme-aktif", "utm_hesabim", false);

                    hesabimSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => {
                            tabGrubuGoster(hesabimSekmeleri, hesabimPanelleri, sekme.dataset.msHesabimTab, "msHesabimTab", "msHesabimPanel", "ms-kod-sekme-aktif", "utm_hesabim");
                        });
                    });
                });

                alanlariSec(kok, "[data-ms-kargo-takip-sekmeleri]").forEach((kargoTakipPaneli) => {
                    if (kargoTakipPaneli.dataset.msKargoTakipSekmeleriHazir === "true") {
                        return;
                    }

                    kargoTakipPaneli.dataset.msKargoTakipSekmeleriHazir = "true";

                    const kargoTakipSekmeleri = Array.from(kargoTakipPaneli.querySelectorAll("[data-ms-kargo-takip-tab]"));
                    const kargoTakipPanelleri = Array.from(kargoTakipPaneli.querySelectorAll("[data-ms-kargo-takip-panel]"));
                    const urlKargoTakipSekmesi = new URLSearchParams(window.location.search).get("utm_kargo_takip");

                    tabGrubuGoster(kargoTakipSekmeleri, kargoTakipPanelleri, urlKargoTakipSekmesi || "kargo-takip", "msKargoTakipTab", "msKargoTakipPanel", "ms-kod-sekme-aktif", "utm_kargo_takip", false);

                    kargoTakipSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => {
                            tabGrubuGoster(kargoTakipSekmeleri, kargoTakipPanelleri, sekme.dataset.msKargoTakipTab, "msKargoTakipTab", "msKargoTakipPanel", "ms-kod-sekme-aktif", "utm_kargo_takip");
                        });
                    });
                });

                alanlariSec(kok, "[data-ms-sayfa-panel='kurumsal'], .ms-kurumsal-sayfa").forEach((kurumsalKok) => {
                    const kurumsalPaneli = kurumsalKok.matches?.(".ms-kurumsal-sayfa") ? kurumsalKok : kurumsalKok.querySelector(".ms-kurumsal-sayfa");

                    if (!kurumsalPaneli || kurumsalPaneli.dataset.msKurumsalSekmeleriHazir === "true") {
                        return;
                    }

                    kurumsalPaneli.dataset.msKurumsalSekmeleriHazir = "true";

                    const kurumsalSekmeleri = Array.from(kurumsalPaneli.querySelectorAll("[data-ms-kurumsal-tab]"));
                    const kurumsalPanelleri = Array.from(kurumsalPaneli.querySelectorAll("[data-ms-kurumsal-panel-kapsayici]"));
                    const urlKurumsalSekmesi = new URLSearchParams(window.location.search).get("utm_kurumsal");
                    const kurumsalMenuAc = kurumsalPaneli.querySelector("[data-ms-kurumsal-menu-ac]");
                    const kurumsalMenu = kurumsalPaneli.querySelector(".ms-kurumsal-yan-menu");
                    const kurumsalMenuPerde = kurumsalPaneli.querySelector(".ms-kurumsal-menu-perde");
                    const kurumsalMenuKapatButonlari = kurumsalPaneli.querySelectorAll("[data-ms-kurumsal-menu-kapat]");

                    const kurumsalMenuDurumunuAyarla = (acik) => {
                        kurumsalMenu?.classList.toggle("ms-kurumsal-yan-menu-acik", acik);
                        kurumsalPaneli.classList.toggle("ms-kurumsal-menu-acik", acik);
                        kurumsalMenuAc?.setAttribute("aria-expanded", acik.toString());
                        kurumsalMenuPerde?.setAttribute("aria-hidden", (!acik).toString());
                        document.body.classList.toggle("ms-hesabim-menu-body-kilitli", acik);
                    };

                    const kurumsalTabGoster = (aktifDeger, urlGuncelle = true) => {
                        const hedefSekme = kurumsalSekmeleri.find((sekme) => sekme.dataset.msKurumsalTab === aktifDeger) || kurumsalSekmeleri[0];
                        const hedefPanel = hedefSekme?.dataset.msKurumsalTab;

                        if (!hedefPanel) {
                            return;
                        }

                        kurumsalSekmeleri.forEach((sekme) => {
                            const aktif = sekme.dataset.msKurumsalTab === hedefPanel;
                            sekme.classList.toggle("ms-kod-sekme-aktif", aktif && sekme.classList.contains("ms-kod-sekme"));
                            sekme.classList.toggle("ms-kurumsal-menu-aktif", aktif && !sekme.classList.contains("ms-kod-sekme"));
                            sekme.setAttribute("aria-pressed", aktif.toString());
                        });

                        kurumsalPanelleri.forEach((panel) => {
                            const aktif = panel.dataset.msKurumsalPanelKapsayici === hedefPanel;
                            panel.classList.toggle("ms-gizli", !aktif);

                            if (aktif) {
                                panelIcerikYukle(panel).then(() => window.msKurumsalSssAkordiyonBaslat?.(panel));
                            }
                        });

                        if (urlGuncelle) {
                            urlParametreGuncelle("utm_kurumsal", hedefPanel);
                        }

                        kurumsalMenuDurumunuAyarla(false);
                    };

                    kurumsalTabGoster(urlKurumsalSekmesi || "hakkimizda", false);

                    kurumsalSekmeleri.forEach((sekme) => {
                        sekme.addEventListener("click", () => kurumsalTabGoster(sekme.dataset.msKurumsalTab));
                    });

                    kurumsalMenuAc?.addEventListener("click", () => kurumsalMenuDurumunuAyarla(true));
                    kurumsalMenuPerde?.addEventListener("click", () => kurumsalMenuDurumunuAyarla(false));
                    kurumsalMenuKapatButonlari.forEach((buton) => {
                        buton.addEventListener("click", () => kurumsalMenuDurumunuAyarla(false));
                    });
                });
            }

            const urlSekmesiHam = new URLSearchParams(window.location.search).get("utm");
            const urlSekmesi = arayuzTablari.has(urlSekmesiHam) ? "arayuz-elementleri" : urlSekmesiHam;
            const baslangicSekmesi = sekmeler.some((sekme) => sekme.dataset.tab === urlSekmesi) ? urlSekmesi : varsayilanProjeSekmesi;

            if (sekmeler.length && panelAlani) {
                projeElementleriTabGoster(baslangicSekmesi, false);
            }

            sekmeler.forEach((sekme) => {
                sekme.addEventListener("click", () => {
                    projeElementleriTabGoster(sekme.dataset.tab);
                });
            });

            projeElementleriKapsamBaslat(projeKok || document);

            document.querySelectorAll("[data-ms-yorum-sekmeleri]").forEach((yorumAlani) => {
                const yorumSekmeleri = Array.from(yorumAlani.querySelectorAll("[data-ms-yorum-tab]"));
                const yorumPanelleri = Array.from(yorumAlani.querySelectorAll("[data-ms-yorum-panel]"));
                const urlYorumSekmesi = new URLSearchParams(window.location.search).get("utm_yorum");

                tabGrubuGoster(yorumSekmeleri, yorumPanelleri, urlYorumSekmesi || "degerlendir", "msYorumTab", "msYorumPanel", "ms-kod-sekme-aktif", "utm_yorum", false);

                yorumSekmeleri.forEach((sekme) => {
                    sekme.addEventListener("click", () => {
                        tabGrubuGoster(yorumSekmeleri, yorumPanelleri, sekme.dataset.msYorumTab, "msYorumTab", "msYorumPanel", "ms-kod-sekme-aktif", "utm_yorum");
                    });
                });
            });

            document.querySelectorAll("[data-ms-kart-link-alani]").forEach((kart) => {
                kart.addEventListener("click", (event) => {
                    if (event.target.closest("a, button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], [data-ms-urun-video], .ms-urun-video-alani, .ms-urun-renk-tooltip-alani, .ms-urun-renk-rozet")) {
                        return;
                    }

                    const link = kart.querySelector("[data-ms-kart-link]");

                    if (!link) {
                        return;
                    }

                    // Sentetik link.click() modifier tasimaz — Ctrl/Cmd+tik yeni sekmede acilir (2026-07-17)
                    if (event.ctrlKey || event.metaKey) {
                        window.open(link.href, "_blank", "noopener");
                        return;
                    }
                    link.click();
                });
            });

            const onayRedModalBaslat = () => {
                let modal = document.querySelector("[data-ms-onay-red-modal]");

                if (!modal) {
                    const sablon = document.querySelector("[data-ms-onay-red-modal-sablon]");

                    if (!(sablon instanceof HTMLTemplateElement)) {
                        return;
                    }

                    const modalHazirlaVeAc = (ayarlar = {}) => {
                        sablon.parentNode?.insertBefore(sablon.content.cloneNode(true), sablon);
                        sablon.remove();
                        onayRedModalBaslat();
                        window.msOnayRedModalAc?.(ayarlar);
                    };

                    window.msOnayRedModalAc = modalHazirlaVeAc;
                    document.addEventListener("click", (event) => {
                        const buton = event.target.closest?.("[data-ms-onay-red-ac]");

                        if (!buton || document.querySelector("[data-ms-onay-red-modal]")) {
                            return;
                        }

                        event.preventDefault();
                        modalHazirlaVeAc({
                            tip: buton.dataset.msOnayRedTip || "onay",
                            baslik: buton.dataset.msOnayRedBaslik,
                            altBaslik: buton.dataset.msOnayRedAltBaslik,
                            metin: buton.dataset.msOnayRedMetin,
                            sure: buton.dataset.msOnayRedSure
                        });
                    });
                    return;
                }

                if (!modal || modal.dataset.msOnayRedModalHazir === "true") {
                    return;
                }

                modal.dataset.msOnayRedModalHazir = "true";

                const kapatButonlari = modal.querySelectorAll("[data-ms-onay-red-modal-kapat]");
                const baslik = modal.querySelector("[data-ms-onay-red-baslik]");
                const altBaslik = modal.querySelector("[data-ms-onay-red-alt-baslik]");
                const metin = modal.querySelector("[data-ms-onay-red-metin]");
                const ikon = modal.querySelector("[data-ms-onay-red-ikon]");
                const sureCizgisi = modal.querySelector("[data-ms-onay-red-sure-cizgisi]");
                let sonOdaklananEleman = null;
                let otomatikKapatTimer = null;

                const modalAc = (ayarlar = {}) => {
                    const tip = ayarlar.tip === "red" ? "red" : "onay";
                    const sure = Number(ayarlar.sure || ayarlar.sureMs || 1000); // 2026-07-17: varsayilan yariya indi (2000→1000)
                    sonOdaklananEleman = document.activeElement;
                    window.clearTimeout(otomatikKapatTimer);
                    modal.classList.remove("ms-onay-red-modal-zamanli");

                    if (baslik) {
                        baslik.textContent = ayarlar.baslik || (tip === "red" ? "Islem Reddedildi" : "Islem Onaylandi");
                    }

                    if (altBaslik) {
                        altBaslik.textContent = ayarlar.altBaslik || (tip === "red" ? "Reddedildi" : "Onaylandi");
                    }

                    if (metin) {
                        metin.textContent = ayarlar.metin || (tip === "red" ? "Islem reddedildi." : "Islem basariyla onaylandi.");
                    }

                    if (ikon) {
                        ikon.classList.toggle("ms-onay-red-modal-ikon-onay", tip !== "red");
                        ikon.classList.toggle("ms-onay-red-modal-ikon-red", tip === "red");
                        ikon.innerHTML = `<i class="fa-solid ${tip === "red" ? "fa-xmark" : "fa-check"} ms-fa-ikon" aria-hidden="true"></i>`;
                    }

                    if (sureCizgisi) {
                        sureCizgisi.style.animation = "none";
                        sureCizgisi.offsetHeight;
                        sureCizgisi.style.animation = "";
                    }

                    modal.style.setProperty("--ms-onay-red-sure", `${sure}ms`);
                    modal.classList.add("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "false");
                    document.body.style.overflow = "hidden";

                    if (sure > 0) {
                        modal.classList.add("ms-onay-red-modal-zamanli");
                        otomatikKapatTimer = window.setTimeout(modalKapat, sure);
                    }

                    window.setTimeout(() => {
                        modal.querySelector(".ms-ornek-modal-aksiyonlar button")?.focus();
                    }, 30);
                };

                const modalKapat = () => {
                    window.clearTimeout(otomatikKapatTimer);
                    modal.classList.remove("ms-onay-red-modal-zamanli");
                    modal.classList.remove("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "true");

                    if (!document.querySelector(".ms-ornek-modal.ms-ornek-modal-acik, .ms-giris-modal.ms-giris-modal-acik")) {
                        document.body.style.overflow = "";
                    }

                    sonOdaklananEleman?.focus?.();
                };

                window.msOnayRedModalAc = modalAc;

                kapatButonlari.forEach((buton) => {
                    buton.addEventListener("click", modalKapat);
                });

                document.addEventListener("click", (event) => {
                    const buton = event.target.closest("[data-ms-onay-red-ac]");

                    if (!buton) {
                        return;
                    }

                    event.preventDefault();
                    modalAc({
                        tip: buton.dataset.msOnayRedTip || "onay",
                        baslik: buton.dataset.msOnayRedBaslik,
                        altBaslik: buton.dataset.msOnayRedAltBaslik,
                        metin: buton.dataset.msOnayRedMetin,
                        sure: buton.dataset.msOnayRedSure
                    });
                });

                document.addEventListener("keydown", (event) => {
                    if (event.key === "Escape" && modal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat();
                    }
                });
            };

            const koleksiyonAkisModallariBaslat = () => {
                const secimModal = document.querySelector("[data-ms-koleksiyon-secim-modal]");
                const varolanModal = document.querySelector("[data-ms-koleksiyon-varolan-modal]");
                const yeniOzetModal = document.querySelector("[data-ms-koleksiyon-yeni-ozet-modal]");

                if (!secimModal || !varolanModal || !yeniOzetModal || secimModal.dataset.msKoleksiyonAkisHazir === "true") {
                    return;
                }

                secimModal.dataset.msKoleksiyonAkisHazir = "true";

                const secimKapatButonlari = secimModal.querySelectorAll("[data-ms-koleksiyon-secim-modal-kapat]");
                const varolanKapatButonlari = varolanModal.querySelectorAll("[data-ms-koleksiyon-varolan-modal-kapat]");
                const yeniOzetKapatButonlari = yeniOzetModal.querySelectorAll("[data-ms-koleksiyon-yeni-ozet-modal-kapat]");
                const yeniButonu = secimModal.querySelector("[data-ms-koleksiyon-secim-yeni]");
                const varolanButonu = secimModal.querySelector("[data-ms-koleksiyon-secim-varolan]");
                const varolanOnayButonu = varolanModal.querySelector("[data-ms-koleksiyon-varolan-onay]");
                const varolanSelect = varolanModal.querySelector("[data-ms-koleksiyon-varolan-select]");
                const varolanUrunGorsel = varolanModal.querySelector("[data-ms-koleksiyon-varolan-urun-gorsel]");
                const varolanUrunAd = varolanModal.querySelector("[data-ms-koleksiyon-varolan-urun-ad]");
                const varolanUrunMeta = varolanModal.querySelector("[data-ms-koleksiyon-varolan-urun-meta]");
                const yeniOzetOnayButonu = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-onay]");
                const yeniOzetUrunGorsel = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-urun-gorsel]");
                const yeniOzetUrunAd = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-urun-ad]");
                const yeniOzetUrunMeta = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-urun-meta]");
                const yeniOzetAd = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-ad]");
                const yeniOzetAciklama = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-aciklama]");
                const yeniOzetHerkeseAcik = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-herkese-acik]");
                const yeniOzetPaylasilabilir = yeniOzetModal.querySelector("[data-ms-koleksiyon-yeni-ozet-paylasilabilir]");
                let aktifUrun = null;
                let sonOdaklananEleman = null;

                const urunBilgisiVarMi = (urunBilgisi) => Boolean(urunBilgisi && (urunBilgisi.id || urunBilgisi.ad || urunBilgisi.gorsel || urunBilgisi.meta));

                const urunBilgisiniTamamla = (urunBilgisi = {}) => {
                    const kaynak = urunBilgisi || {};

                    return {
                        id: kaynak.id || `koleksiyon-urun-${Date.now()}`,
                        ad: kaynak.ad || "Urun",
                        gorsel: kaynak.gorsel || "/images/ornek-resim.jpg",
                        meta: kaynak.meta || "Urun bilgisi"
                    };
                };

                const modalAc = (modal) => {
                    sonOdaklananEleman = document.activeElement;
                    modal.classList.add("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "false");
                    document.body.style.overflow = "hidden";
                };

                const modalKapat = (modal) => {
                    modal.classList.remove("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "true");

                    if (!document.querySelector(".ms-ornek-modal.ms-ornek-modal-acik, .ms-giris-modal.ms-giris-modal-acik")) {
                        document.body.style.overflow = "";
                    }

                    sonOdaklananEleman?.focus?.();
                };

                const varolanUrunuGuncelle = () => {
                    const urun = urunBilgisiniTamamla(aktifUrun);

                    if (varolanUrunGorsel) {
                        varolanUrunGorsel.src = urun.gorsel;
                        varolanUrunGorsel.alt = urun.ad;
                    }

                    if (varolanUrunAd) {
                        varolanUrunAd.textContent = urun.ad;
                    }

                    if (varolanUrunMeta) {
                        varolanUrunMeta.textContent = urun.meta;
                    }
                };

                const yeniOzetUrunuGuncelle = () => {
                    const urun = urunBilgisiniTamamla(aktifUrun);

                    if (yeniOzetUrunGorsel) {
                        yeniOzetUrunGorsel.src = urun.gorsel;
                        yeniOzetUrunGorsel.alt = urun.ad;
                    }

                    if (yeniOzetUrunAd) {
                        yeniOzetUrunAd.textContent = urun.ad;
                    }

                    if (yeniOzetUrunMeta) {
                        yeniOzetUrunMeta.textContent = urun.meta;
                    }
                };

                const secenekMetniAl = (secenek) => secenek?.querySelector("[data-ms-ozel-select-metin]")?.textContent?.trim()
                    || secenek?.textContent?.trim()
                    || "";

                const secimModalAc = (urunBilgisi) => {
                    sonOdaklananEleman = document.activeElement;
                    aktifUrun = urunBilgisiVarMi(urunBilgisi) ? urunBilgisiniTamamla(urunBilgisi) : null;

                    if (!aktifUrun) {
                        return;
                    }

                    modalAc(secimModal);
                };

                const varolanModalAc = () => {
                    varolanUrunuGuncelle();
                    modalKapat(secimModal);
                    modalAc(varolanModal);
                    window.setTimeout(() => {
                        varolanModal.querySelector("[data-ms-ozel-select-tetikleyici]")?.focus();
                    }, 40);
                };

                const yeniKoleksiyonModalAc = () => {
                    yeniOzetUrunuGuncelle();
                    if (secimModal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat(secimModal);
                    }
                    modalAc(yeniOzetModal);
                    window.setTimeout(() => yeniOzetAd?.focus(), 40);
                };

                const yeniKoleksiyonOlustur = () => {
                    const urun = urunBilgisiniTamamla(aktifUrun);
                    const ad = yeniOzetAd?.value.trim() || "Yeni Koleksiyon";
                    const aciklama = yeniOzetAciklama?.value.trim() || "Yeni seçilen ürünle oluşturulan alışveriş koleksiyonu.";
                    const olay = new CustomEvent("ms:koleksiyon-olustur", {
                        bubbles: true,
                        cancelable: true,
                        detail: {
                            ad,
                            aciklama,
                            herkeseAcik: Boolean(yeniOzetHerkeseAcik?.checked),
                            paylasilabilir: Boolean(yeniOzetPaylasilabilir?.checked),
                            urunler: aktifUrun ? [urun] : [],
                            modal: yeniOzetModal,
                            kapat: () => modalKapat(yeniOzetModal)
                        }
                    });

                    if (yeniOzetModal.dispatchEvent(olay)) {
                        modalKapat(yeniOzetModal);
                    }
                };

                const varolanKoleksiyonaEkle = () => {
                    const seciliKoleksiyonlar = Array.from(varolanModal.querySelectorAll("[data-ms-koleksiyon-varolan-select] .ms-ozel-select-secenek-aktif"))
                        .map(secenekMetniAl)
                        .filter(Boolean);
                    const olay = new CustomEvent("ms:koleksiyon-varolan-ekle", {
                        bubbles: true,
                        detail: {
                            urun: urunBilgisiniTamamla(aktifUrun),
                            koleksiyonlar: seciliKoleksiyonlar,
                            modal: varolanModal
                        }
                    });

                    varolanModal.dispatchEvent(olay);
                    modalKapat(varolanModal);
                };

                window.msKoleksiyonAkisBaslat = secimModalAc;
                window.msKoleksiyonVarolanModalAc = (urunBilgisi) => {
                    aktifUrun = urunBilgisiniTamamla(urunBilgisi);
                    varolanModalAc();
                };

                secimKapatButonlari.forEach((buton) => {
                    buton.addEventListener("click", () => modalKapat(secimModal));
                });

                varolanKapatButonlari.forEach((buton) => {
                    buton.addEventListener("click", () => modalKapat(varolanModal));
                });

                yeniOzetKapatButonlari.forEach((buton) => {
                    buton.addEventListener("click", () => modalKapat(yeniOzetModal));
                });

                yeniButonu?.addEventListener("click", yeniKoleksiyonModalAc);
                varolanButonu?.addEventListener("click", varolanModalAc);
                varolanOnayButonu?.addEventListener("click", varolanKoleksiyonaEkle);
                yeniOzetOnayButonu?.addEventListener("click", yeniKoleksiyonOlustur);
                document.addEventListener("keydown", (event) => {
                    if (event.key !== "Escape") {
                        return;
                    }

                    if (yeniOzetModal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat(yeniOzetModal);
                    } else if (varolanModal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat(varolanModal);
                    } else if (secimModal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat(secimModal);
                    }
                });
            };

            const koleksiyonModallariBaslat = (kok = document) => {
            alanlariSec(kok, "[data-ms-koleksiyon-modal]").forEach((modal) => {
                if (modal.dataset.msKoleksiyonModalHazir === "true") {
                    return;
                }

                modal.dataset.msKoleksiyonModalHazir = "true";

                const modalKapsam = modal.closest(".ms-hesabim-sayfa") || document;
                const acButonlari = modalKapsam.querySelectorAll("[data-ms-koleksiyon-modal-ac]");
                const kapatButonlari = modal.querySelectorAll("[data-ms-koleksiyon-modal-kapat]");
                const sekmeler = Array.from(modal.querySelectorAll("[data-ms-koleksiyon-sekme]"));
                const paneller = Array.from(modal.querySelectorAll("[data-ms-koleksiyon-panel]"));
                const urunButonlari = Array.from(modal.querySelectorAll("[data-ms-koleksiyon-urun]"));
                const seciliListe = modal.querySelector("[data-ms-koleksiyon-secili-liste]");
                const seciliBos = modal.querySelector("[data-ms-koleksiyon-secili-bos]");
                const seciliSayaclar = modal.querySelectorAll("[data-ms-koleksiyon-secili-sayac]");
                const aramaInput = modal.querySelector("[data-ms-koleksiyon-arama]");
                const aramaSonuc = modal.querySelector("[data-ms-koleksiyon-arama-sonuc]");
                const aramaBos = modal.querySelector("[data-ms-koleksiyon-arama-bos]");
                const olusturButonu = modal.querySelector("[data-ms-koleksiyon-olustur]");
                const secilenUrunler = new Map();
                let sonOdaklananEleman = null;

                const metniGuvenliYap = (metin) => (metin || "")
                    .replace(/&/g, "&amp;")
                    .replace(/</g, "&lt;")
                    .replace(/>/g, "&gt;")
                    .replace(/"/g, "&quot;")
                    .replace(/'/g, "&#039;");

                const hariciUrunSec = (urunBilgisi) => {
                    if (!urunBilgisi || !(urunBilgisi.id || urunBilgisi.ad || urunBilgisi.gorsel)) {
                        return;
                    }

                    const id = urunBilgisi.id || `harici-${Date.now()}`;
                    secilenUrunler.set(id, {
                        id,
                        ad: urunBilgisi.ad || "Ürün",
                        gorsel: urunBilgisi.gorsel || "/images/ornek-resim.jpg",
                        meta: urunBilgisi.meta || "Ürün kartı"
                    });
                    seciliListeyiGuncelle();
                };

                const hariciUrunleriSec = (urunler) => {
                    (Array.isArray(urunler) ? urunler : [urunler]).forEach(hariciUrunSec);
                };

                const tetikleyiciUrunBilgisiOlustur = (buton) => {
                    const sepetGrubu = buton.closest(".ms-sepet-satici-grubu");
                    const sepetSatiri = sepetGrubu?.querySelector(".ms-sepet-satiri input[type='checkbox']:checked")?.closest(".ms-sepet-satiri")
                        || sepetGrubu?.querySelector(".ms-sepet-satiri");

                    if (!sepetSatiri) {
                        return null;
                    }

                    const baslik = sepetSatiri.querySelector(".ms-sepet-basligi")?.textContent?.trim();
                    const gorsel = sepetSatiri.querySelector(".ms-sepet-gorsel, img");
                    const gorselYolu = gorsel?.currentSrc
                        || gorsel?.getAttribute("src")
                        || gorsel?.getAttribute("data-ms-lazy-src")
                        || "/images/ornek-resim.jpg";
                    const fiyat = sepetSatiri.querySelector(".ms-urun-fiyat, [data-ms-sepet-satir-tutar]")?.textContent?.trim();

                    return {
                        // 2026-07-17: gercek urun kodu oncelikli (API productCodes'a gider)
                        id: sepetSatiri.dataset.msUrunKod || sepetSatiri.dataset.msSepetSatir || `sepet-${Date.now()}`,
                        ad: baslik || "Sepet urunu",
                        gorsel: gorselYolu,
                        meta: fiyat ? `Sepet - ${fiyat}` : "Sepet urunu"
                    };
                };

                const tetikleyiciSepetUrunleriniOlustur = (buton) => {
                    const sepetGrubu = buton.closest(".ms-sepet-satici-grubu");

                    if (!sepetGrubu) {
                        return [];
                    }

                    const seciliSatirlar = Array.from(sepetGrubu.querySelectorAll(".ms-sepet-satiri"))
                        .filter((satir) => satir.querySelector("input[type='checkbox']")?.checked);
                    const satirlar = seciliSatirlar.length ? seciliSatirlar : Array.from(sepetGrubu.querySelectorAll(".ms-sepet-satiri"));

                    return satirlar.map((satir, index) => {
                        const baslik = satir.querySelector(".ms-sepet-basligi")?.textContent?.trim();
                        const gorsel = satir.querySelector(".ms-sepet-gorsel, img");
                        const gorselYolu = gorsel?.currentSrc
                            || gorsel?.getAttribute("src")
                            || gorsel?.getAttribute("data-ms-lazy-src")
                            || "/images/ornek-resim.jpg";
                        const fiyat = satir.querySelector(".ms-urun-fiyat, [data-ms-sepet-satir-tutar]")?.textContent?.trim();

                        return {
                            // 2026-07-17: gercek urun kodu oncelikli (API productCodes'a gider)
                            id: satir.dataset.msUrunKod || satir.dataset.msSepetSatir || `sepet-${index}-${Date.now()}`,
                            ad: baslik || "Sepet urunu",
                            gorsel: gorselYolu,
                            meta: fiyat ? `Sepet - ${fiyat}` : "Sepet urunu"
                        };
                    });
                };

                const modalAc = (urunBilgisi, secimiSifirla = false) => {
                    sonOdaklananEleman = document.activeElement;

                    if (secimiSifirla) {
                        secilenUrunler.clear();
                    }

                    hariciUrunleriSec(urunBilgisi);
                    modal.classList.add("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "false");
                    document.body.style.overflow = "hidden";
                    window.setTimeout(() => {
                        modal.querySelector("[data-ms-koleksiyon-ad]")?.focus();
                    }, 40);
                };

                const modalKapat = () => {
                    modal.classList.remove("ms-ornek-modal-acik");
                    modal.setAttribute("aria-hidden", "true");
                    document.body.style.overflow = "";
                    sonOdaklananEleman?.focus?.();
                };

                modal.msKoleksiyonModalAc = modalAc;
                window.msKoleksiyonModalAc = modalAc;

                const sekmeGoster = (sekmeAdi) => {
                    sekmeler.forEach((sekme) => {
                        const aktif = sekme.dataset.msKoleksiyonSekme === sekmeAdi;
                        sekme.classList.toggle("ms-koleksiyon-sekme-aktif", aktif);
                        sekme.setAttribute("aria-selected", aktif.toString());
                    });

                    paneller.forEach((panel) => {
                        const aktif = panel.dataset.msKoleksiyonPanel === sekmeAdi;
                        panel.classList.toggle("ms-koleksiyon-panel-aktif", aktif);
                        panel.hidden = !aktif;
                    });
                };

                const urunButonDurumlariniGuncelle = () => {
                    urunButonlari.forEach((buton) => {
                        const secili = secilenUrunler.has(buton.dataset.urunId);
                        buton.classList.toggle("ms-koleksiyon-urun-secili", secili);
                        buton.setAttribute("aria-pressed", secili.toString());
                    });
                };

                const seciliListeyiGuncelle = () => {
                    if (!seciliListe) {
                        return;
                    }

                    seciliListe.innerHTML = "";
                    const urunler = Array.from(secilenUrunler.values());
                    seciliBos.hidden = urunler.length > 0;
                    seciliListe.hidden = urunler.length === 0;
                    seciliSayaclar.forEach((sayac) => {
                        sayac.textContent = urunler.length.toString();
                    });

                    urunler.forEach((urun) => {
                        const kart = document.createElement("article");
                        kart.className = "ms-koleksiyon-secili-kart";
                        kart.innerHTML = `
                            <img src="${metniGuvenliYap(urun.gorsel)}" alt="${metniGuvenliYap(urun.ad)}" />
                            <span>
                                <strong>${metniGuvenliYap(urun.ad)}</strong>
                                <small>${metniGuvenliYap(urun.meta)}</small>
                            </span>
                            <button type="button" aria-label="${metniGuvenliYap(urun.ad)} ürününü seçimden kaldır" data-ms-koleksiyon-secili-kaldir="${metniGuvenliYap(urun.id)}">
                                <i class="fa-solid fa-xmark ms-fa-ikon" aria-hidden="true"></i>
                            </button>`;
                        seciliListe.appendChild(kart);
                    });

                    urunButonDurumlariniGuncelle();
                };

                const urunSec = (buton) => {
                    const id = buton.dataset.urunId;

                    if (!id || secilenUrunler.has(id)) {
                        return;
                    }

                    secilenUrunler.set(id, {
                        id,
                        ad: buton.dataset.urunAd || "Ürün",
                        gorsel: buton.dataset.urunGorsel || "",
                        meta: buton.dataset.urunMeta || "Koleksiyon"
                    });
                    seciliListeyiGuncelle();
                };

                const urunKaldir = (id) => {
                    secilenUrunler.delete(id);
                    seciliListeyiGuncelle();
                };

                const aramaSonuclariniGuncelle = () => {
                    if (!aramaInput || !aramaSonuc || !aramaBos) {
                        return;
                    }

                    const sorgu = aramaInput.value.trim().toLocaleLowerCase("tr-TR");
                    const aramaUrunleri = Array.from(modal.querySelectorAll("[data-ms-koleksiyon-arama-urun]"));
                    let gorunenAdet = 0;

                    aramaUrunleri.forEach((urun) => {
                        const ad = (urun.dataset.urunAd || "").toLocaleLowerCase("tr-TR");
                        const gorunsun = sorgu.length > 0 && ad.includes(sorgu);
                        urun.hidden = !gorunsun;

                        if (gorunsun) {
                            gorunenAdet += 1;
                        }
                    });

                    aramaSonuc.hidden = sorgu.length === 0 || gorunenAdet === 0;
                    aramaBos.hidden = sorgu.length > 0 && gorunenAdet > 0;
                    aramaBos.querySelector("strong").textContent = sorgu.length === 0
                        ? "Arama yaparak ürün ekleyin"
                        : "Arama sonucu bulunamadı";
                    aramaBos.querySelector("p").textContent = sorgu.length === 0
                        ? "Ürün adı yazdığınızda örnek sonuçlar burada listelenecek."
                        : "Farklı bir ürün adı yazarak tekrar deneyin.";
                };

                const koleksiyonOlusturIste = () => {
                    const adInput = modal.querySelector("[data-ms-koleksiyon-ad]");
                    const aciklamaInput = modal.querySelector("[data-ms-koleksiyon-aciklama]");
                    const herkeseAcik = modal.querySelector("[data-ms-koleksiyon-herkese-acik]")?.checked;
                    const paylasilabilir = modal.querySelector("[data-ms-koleksiyon-paylasilabilir]")?.checked;
                    const ad = adInput?.value.trim() || "Yeni Koleksiyon";
                    const aciklama = aciklamaInput?.value.trim() || "Yeni seçilen ürünlerle oluşturulan alışveriş koleksiyonu.";
                    const urunler = Array.from(secilenUrunler.values());

                    const olay = new CustomEvent("ms:koleksiyon-olustur", {
                        bubbles: true,
                        cancelable: true,
                        detail: {
                            ad,
                            aciklama,
                            herkeseAcik: Boolean(herkeseAcik),
                            paylasilabilir: Boolean(paylasilabilir),
                            urunler,
                            modal,
                            kapat: modalKapat
                        }
                    });

                    if (modal.dispatchEvent(olay)) {
                        modalKapat();
                    }
                };

                acButonlari.forEach((buton) => {
                    buton.addEventListener("click", () => {
                        const sepetUrunleri = tetikleyiciSepetUrunleriniOlustur(buton);

                        if (sepetUrunleri.length) {
                            modalAc(sepetUrunleri, true);
                            sekmeGoster("sectiklerim");
                            return;
                        }

                        const urunBilgisi = tetikleyiciUrunBilgisiOlustur(buton);

                        if (urunBilgisi && typeof window.msKoleksiyonAkisBaslat === "function") {
                            window.msKoleksiyonAkisBaslat(urunBilgisi);
                            return;
                        }

                        modalAc(urunBilgisi);
                    });
                });

                kapatButonlari.forEach((buton) => {
                    buton.addEventListener("click", modalKapat);
                });

                sekmeler.forEach((sekme) => {
                    sekme.addEventListener("click", () => sekmeGoster(sekme.dataset.msKoleksiyonSekme));
                });

                urunButonlari.forEach((buton) => {
                    buton.setAttribute("aria-pressed", "false");
                    buton.addEventListener("click", () => urunSec(buton));
                });

                urunButonlari
                    .filter((buton) => buton.hasAttribute("data-ms-koleksiyon-urun-secili"))
                    .forEach((buton) => urunSec(buton));

                seciliListe?.addEventListener("click", (event) => {
                    const kaldirButonu = event.target.closest("[data-ms-koleksiyon-secili-kaldir]");

                    if (kaldirButonu) {
                        urunKaldir(kaldirButonu.dataset.msKoleksiyonSeciliKaldir);
                    }
                });

                aramaInput?.addEventListener("input", aramaSonuclariniGuncelle);
                olusturButonu?.addEventListener("click", koleksiyonOlusturIste);
                seciliListeyiGuncelle();
                aramaSonuclariniGuncelle();

                document.addEventListener("keydown", (event) => {
                    if (event.key === "Escape" && modal.classList.contains("ms-ornek-modal-acik")) {
                        modalKapat();
                    }
                });
            });
            };

            const hesapStatuKartlariBaslat = (kok = document) => {
                alanlariSec(kok, "[data-ms-hesap-statu-toggle]").forEach((buton) => {
                    if (buton.dataset.msHesapStatuToggleHazir === "true") {
                        return;
                    }

                    buton.dataset.msHesapStatuToggleHazir = "true";
                    const kart = buton.closest(".ms-hesap-statu-katlanabilir");
                    const detay = kart?.querySelector("[data-ms-hesap-statu-detay]");
                    const ikon = buton.querySelector(".ms-fa-ikon");

                    if (!kart || !detay) {
                        return;
                    }

                    const durumAyarla = (acik) => {
                        detay.hidden = !acik;
                        buton.setAttribute("aria-expanded", acik.toString());
                        ikon?.classList.toggle("fa-chevron-up", acik);
                        ikon?.classList.toggle("fa-chevron-down", !acik);
                    };

                    durumAyarla(buton.getAttribute("aria-expanded") === "true");
                    buton.addEventListener("click", () => durumAyarla(detay.hidden));
                });
            };

            window.msKoleksiyonModallariBaslat = koleksiyonModallariBaslat;
            window.msKoleksiyonAkisModallariBaslat = koleksiyonAkisModallariBaslat;
            window.msHesapStatuKartlariBaslat = hesapStatuKartlariBaslat;
            onayRedModalBaslat();
            koleksiyonAkisModallariBaslat();
            koleksiyonModallariBaslat();
            hesapStatuKartlariBaslat();

            const infiniteOrnekleriBaslat = (kok = document) => {
                const lazyInfiniteSecici = ".lazy-infinite-on";
                const ornekler = [];

                if (kok?.matches?.("[data-ms-infinite-ornek]")) {
                    ornekler.push(kok);
                }

                kok?.querySelectorAll?.("[data-ms-infinite-ornek]").forEach((ornek) => {
                    ornekler.push(ornek);
                });

                ornekler.filter((ornek) => Boolean(ornek.closest(lazyInfiniteSecici))).forEach((ornek) => {
                if (ornek.dataset.msInfiniteHazir === "true") {
                    return;
                }

                ornek.dataset.msInfiniteHazir = "true";
                const liste = ornek.querySelector("[data-ms-infinite-liste]");
                const yukleniyor = ornek.querySelector("[data-ms-infinite-yukleniyor]");
                const template = ornek.querySelector("[data-ms-infinite-template]");
                const hazirKartlar = liste ? Array.from(liste.querySelectorAll("[data-ms-infinite-kart]")) : [];
                const kayitlar = Array.from({ length: 26 }, (_, index) => ({
                    baslik: `Kayıt ${index + 1}`,
                    metin: `Bu kayıt Infinite Scroll örneği için oluşturuldu. Liste yüzde 80 seviyesine geldiğinde sonraki kayıtlar alta eklenir.`
                }));
                let gosterilen = 0;
                let yuklemeVar = false;

                if (!liste) {
                    return;
                }

                if (template) {
                    const toplam = parseInt(ornek.dataset.msInfiniteToplam || "100", 10);
                    const ilkAdet = parseInt(ornek.dataset.msInfiniteIlk || "20", 10);
                    const sayfaAdedi = parseInt(liste.dataset.msInfiniteAdet || ornek.dataset.msInfiniteAdet || "20", 10);
                    const infiniteAnahtari = `ms-infinite-adet:${window.location.pathname}${window.location.search}:${ornek.closest("[data-panel]")?.dataset.panel || "genel"}:${toplam}`;
                    const kayitliAdet = Number(sessionStorage.getItem(infiniteAnahtari) || 0);
                    const mobilGorunum = window.matchMedia("(max-width: 1023px)").matches;
                    const sadeceIlkYukle = ornek.dataset.msInfiniteSadeceIlk === "true";
                    const baslangicAdedi = sadeceIlkYukle ? ilkAdet : (mobilGorunum ? ilkAdet : Math.min(Math.max(ilkAdet, kayitliAdet), toplam));
                    let uretilen = 0;
                    let templateYuklemeVar = false;

                    const kartTiklamasiniHazirla = (kart) => {
                        if (!kart || kart.dataset.msKartLinkHazir === "true") {
                            return;
                        }

                        kart.dataset.msKartLinkHazir = "true";
                        kart.addEventListener("click", (event) => {
                            if (event.target.closest("a, button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], [data-ms-urun-video], .ms-urun-video-alani, .ms-urun-renk-tooltip-alani, .ms-urun-renk-rozet")) {
                                return;
                            }

                            const link = kart.querySelector("[data-ms-kart-link]");
                            if (!link) {
                                return;
                            }
                            // Sentetik link.click() modifier tasimaz — Ctrl/Cmd+tik yeni sekmede acilir (2026-07-17)
                            if (event.ctrlKey || event.metaKey) {
                                window.open(link.href, "_blank", "noopener");
                                return;
                            }
                            link.click();
                        });
                    };

                    const kartParcasiOlustur = (sira) => {
                        const parca = template.content.cloneNode(true);

                        parca.querySelectorAll("img").forEach((img) => {
                            const src = img.getAttribute("src");
                            const lazyDisi =
                                img.classList.contains("no-lazy") ||
                                img.classList.contains("ms-urun-kampanya-etiketi") ||
                                img.closest(".ms-urun-gorsel-etiketleri");

                            if (src && !lazyDisi) {
                                img.dataset.msLazySrc = src;
                                img.removeAttribute("src");
                            }

                            if (img.classList.contains("ms-urun-gorsel")) {
                                img.dataset.msLazySkeleton = "true";
                            }
                        });

                        parca.querySelectorAll("video").forEach((video) => {
                            video.preload = "none";
                            video.setAttribute("preload", "none");
                        });

                        parca.querySelectorAll("[data-ms-kart-link-alani]").forEach((kart) => {
                            kart.dataset.msInfiniteKart = sira.toString();
                            kartTiklamasiniHazirla(kart);
                        });

                        return parca;
                    };

                    const templateKartEkle = (adet = sayfaAdedi) => {
                        if (templateYuklemeVar || uretilen >= toplam) {
                            return;
                        }

                        templateYuklemeVar = true;
                        yukleniyor?.classList.add("ms-aktif");

                        window.setTimeout(() => {
                            const parca = document.createDocumentFragment();
                            const hedef = Math.min(uretilen + adet, toplam);

                            for (let sira = uretilen + 1; sira <= hedef; sira += 1) {
                                parca.appendChild(kartParcasiOlustur(sira));
                            }

                            liste.appendChild(parca);
                            uretilen = hedef;
                            sessionStorage.setItem(infiniteAnahtari, String(uretilen));
                            templateYuklemeVar = false;
                            yukleniyor?.classList.remove("ms-aktif");
                            window.msUrunKartDavranislariYenile?.(liste);
                            window.msLazyLoadYenile?.(liste);
                            window.msProjeElementleriScrollGeriYukle?.();
                        }, 80);
                    };

                    const templateKontrolEt = () => {
                        if (templateYuklemeVar || uretilen >= toplam) {
                            return;
                        }

                        const listeRect = liste.getBoundingClientRect();
                        const listeBaslangic = listeRect.top + window.scrollY;
                        const listeYukseklik = Math.max(liste.offsetHeight, 1);
                        const gorunenAlt = window.scrollY + window.innerHeight;
                        const listeIlerlemesi = (gorunenAlt - listeBaslangic) / listeYukseklik;

                        if (listeIlerlemesi >= 0.8) {
                            templateKartEkle();
                        }
                    };

                    templateKartEkle(baslangicAdedi);
                    window.addEventListener("scroll", templateKontrolEt, { passive: true });
                    window.addEventListener("resize", templateKontrolEt);
                    if (!sadeceIlkYukle) {
                        window.setTimeout(templateKontrolEt, 140);
                    }
                    return;
                }

                if (hazirKartlar.length) {
                    const sayfaAdedi = parseInt(liste.dataset.msInfiniteAdet || ornek.dataset.msInfiniteAdet || "20", 10);
                    let hazirYuklemeVar = false;

                    const gizliKartlariGetir = () => hazirKartlar.filter((kart) => kart.classList.contains("ms-infinite-kart-gizli"));

                    const hazirKartEkle = (adet = sayfaAdedi) => {
                        const gizliKartlar = gizliKartlariGetir();

                        if (hazirYuklemeVar || !gizliKartlar.length) {
                            return;
                        }

                        hazirYuklemeVar = true;
                        yukleniyor?.classList.add("ms-aktif");

                        window.setTimeout(() => {
                            gizliKartlar.slice(0, adet).forEach((kart) => {
                                kart.classList.remove("ms-infinite-kart-gizli");
                            });
                            hazirYuklemeVar = false;
                            yukleniyor?.classList.remove("ms-aktif");
                            window.msLazyLoadYenile?.(liste);
                        }, 180);
                    };

                    const hazirKontrolEt = () => {
                        const listeKaydirilabilir = liste.scrollHeight > liste.clientHeight + 2;

                        if (listeKaydirilabilir) {
                            const esik = liste.scrollHeight * 0.8;

                            if (liste.scrollTop + liste.clientHeight >= esik) {
                                hazirKartEkle();
                            }

                            return;
                        }

                        const listeAlt = liste.getBoundingClientRect().bottom;

                        if (listeAlt <= window.innerHeight * 1.2) {
                            hazirKartEkle();
                        }
                    };

                    liste.addEventListener("scroll", hazirKontrolEt, { passive: true });
                    window.addEventListener("scroll", hazirKontrolEt, { passive: true });
                    window.addEventListener("resize", hazirKontrolEt);
                    hazirKontrolEt();
                    return;
                }

                const kartHtml = (kayit) => `<article class="ms-infinite-ornek-kart"><strong>${kayit.baslik}</strong><p>${kayit.metin}</p></article>`;

                const kayitEkle = (adet = 6) => {
                    if (yuklemeVar || gosterilen >= kayitlar.length) {
                        return;
                    }

                    yuklemeVar = true;
                    yukleniyor?.classList.add("ms-aktif");

                    window.setTimeout(() => {
                        const siradaki = kayitlar.slice(gosterilen, gosterilen + adet);
                        liste.insertAdjacentHTML("beforeend", siradaki.map(kartHtml).join(""));
                        gosterilen += siradaki.length;
                        yuklemeVar = false;
                        yukleniyor?.classList.remove("ms-aktif");
                    }, 180);
                };

                const kontrolEt = () => {
                    const esik = liste.scrollHeight * 0.8;

                    if (liste.scrollTop + liste.clientHeight >= esik) {
                        kayitEkle();
                    }
                };

                kayitEkle(10);
                liste.addEventListener("scroll", kontrolEt, { passive: true });
            });
            };

            window.msInfiniteOrnekleriBaslat = infiniteOrnekleriBaslat;
            window.msRegisterPageModule?.("infinite-scroll-ornek", infiniteOrnekleriBaslat);
            infiniteOrnekleriBaslat();

            butonBoyutlari.forEach((buton) => {
                buton.addEventListener("click", () => {
                    const secilenBoyut = buton.dataset.buttonSize;

                    butonBoyutlari.forEach((oge) => {
                        const aktif = oge === buton;
                        oge.setAttribute("aria-pressed", aktif.toString());
                        oge.classList.toggle("ms-buton-boyut-aktif", aktif);
                    });

                    ornekButonlar.forEach((ornek) => {
                        ornek.classList.remove(...boyutClasslari);
                        ornek.classList.add(secilenBoyut);
                    });
                });
            });

            const filtreBloklariBaslat = (kok = document) => {
                kok.querySelectorAll("[data-filter-block]").forEach((filtre) => {
                if (filtre.dataset.msFiltreHazir === "true") {
                    return;
                }

                filtre.dataset.msFiltreHazir = "true";
                const buton = filtre.querySelector("[data-filter-toggle]");
                const icerik = filtre.querySelector("[data-filter-content]");
                const ok = filtre.querySelector(".ms-filtre-ok");
                const arama = filtre.querySelector("[data-filter-search]");
                const secimler = filtre.querySelectorAll("[data-filter-option]");
                const uygulaButonlari = filtre.querySelectorAll(".ms-filtre-uygula-buton");

                if (!buton || !icerik || !ok) {
                    return;
                }

                secimler.forEach((secim, index) => {
                    if (!secim.dataset.msFiltreSira) {
                        secim.dataset.msFiltreSira = index.toString();
                    }
                });

                const seciliSecenekleriUsteTasi = () => {
                    const kapsayicilar = Array.from(new Set(Array.from(secimler).map((secim) => secim.parentElement).filter(Boolean)));

                    kapsayicilar.forEach((kapsayici) => {
                        Array.from(kapsayici.querySelectorAll("[data-filter-option]"))
                            .sort((ilk, ikinci) => {
                                const ilkSecili = ilk.querySelector("input")?.checked ? 0 : 1;
                                const ikinciSecili = ikinci.querySelector("input")?.checked ? 0 : 1;

                                if (ilkSecili !== ikinciSecili) {
                                    return ilkSecili - ikinciSecili;
                                }

                                return Number(ilk.dataset.msFiltreSira || 0) - Number(ikinci.dataset.msFiltreSira || 0);
                            })
                            .forEach((secim) => kapsayici.appendChild(secim));
                    });
                };

                buton.addEventListener("click", () => {
                    const acik = buton.getAttribute("aria-expanded") === "true";
                    if (!acik) {
                        // 2026-07-17: akordeon — ayni kapsayicidaki diger filtre gruplari kapanir
                        (filtre.parentElement || document).querySelectorAll("[data-filter-block]").forEach((diger) => {
                            if (diger === filtre) { return; }
                            const digerButon = diger.querySelector("[data-filter-toggle]");
                            if (digerButon?.getAttribute("aria-expanded") !== "true") { return; }
                            digerButon.setAttribute("aria-expanded", "false");
                            diger.querySelector("[data-filter-content]")?.classList.add("ms-gizli");
                            diger.querySelector(".ms-filtre-ok")?.classList.remove("ms-filtre-ok-acik");
                        });
                    }
                    buton.setAttribute("aria-expanded", (!acik).toString());
                    icerik.classList.toggle("ms-gizli", acik);
                    ok.classList.toggle("ms-filtre-ok-acik", !acik);
                    if (!acik) {
                        // 2026-07-17: acilan grup gorunur alana kaydirilir — uzun filtre
                        // listesinde acilan seceneklerin ekran disinda kalmamasi icin.
                        window.requestAnimationFrame(() => {
                            filtre.scrollIntoView({ behavior: "smooth", block: "nearest" });
                        });
                    }
                });

                if (arama) {
                    arama.addEventListener("input", () => {
                        const aranan = arama.value.trim().toLocaleLowerCase("tr-TR");

                        secimler.forEach((secim) => {
                            const metin = secim.textContent.trim().toLocaleLowerCase("tr-TR");
                            secim.hidden = !metin.includes(aranan);
                        });
                    });
                }

                uygulaButonlari.forEach((uygulaButonu) => {
                    uygulaButonu.addEventListener("click", seciliSecenekleriUsteTasi);
                });
            });

                kok.querySelectorAll("[data-ms-filtre-tumunu-temizle]").forEach((temizleButonu) => {
                    if (temizleButonu.dataset.msFiltreTemizleHazir === "true") {
                        return;
                    }

                    temizleButonu.dataset.msFiltreTemizleHazir = "true";
                    temizleButonu.addEventListener("click", () => {
                        const filtreAlani = temizleButonu.closest(".ms-urun-listesi-sayfa") || document;

                        filtreAlani.querySelectorAll("input[type='checkbox']:checked, input[type='radio']:checked").forEach((input) => {
                            input.checked = false;
                            input.dispatchEvent(new Event("change", { bubbles: true }));
                        });

                        filtreAlani.querySelectorAll("input[type='search'], input[type='number']").forEach((input) => {
                            if (!input.value) {
                                return;
                            }

                            input.value = "";
                            input.dispatchEvent(new Event("input", { bubbles: true }));
                        });

                        filtreAlani.querySelectorAll("[data-ms-mobil-hizli-filtre].ms-urun-listesi-mobil-chip-secili").forEach((buton) => {
                            buton.click();
                        });

                        filtreAlani.dispatchEvent(new CustomEvent("ms:filtreler-temizlendi", { bubbles: true }));
                    });
                });
            };

            window.msFiltreBloklariBaslat = filtreBloklariBaslat;
            filtreBloklariBaslat();


            const siralamaSelectleriBaslat = (kok = document) => {
                kok.querySelectorAll("[data-ms-siralama-select]").forEach((select) => {
                if (select.dataset.msSiralamaHazir === "true") {
                    return;
                }

                select.dataset.msSiralamaHazir = "true";
                const tetikleyici = select.querySelector("[data-ms-siralama-tetikleyici]");
                const deger = select.querySelector("[data-ms-siralama-deger]");
                const secenekler = select.querySelectorAll("[data-ms-siralama-secenek]");

                if (!tetikleyici || !deger) {
                    return;
                }

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
                        const secenekMetni = secenek.textContent.trim();
                        deger.textContent = secenekMetni;

                        secenekler.forEach((oge) => {
                            const aktif = oge === secenek;
                            oge.classList.toggle("ms-siralama-select-secenek-aktif", aktif);
                            oge.setAttribute("aria-selected", aktif.toString());
                        });

                        kapat();
                    });
                });

                document.addEventListener("click", (event) => {
                    if (!select.contains(event.target)) {
                        kapat();
                    }
                });
            });
            };

            window.msSiralamaSelectleriBaslat = siralamaSelectleriBaslat;
            siralamaSelectleriBaslat();

            const ozelSelectleriBaslat = (kok = document) => {
            const selectler = [];

            if (kok instanceof Element && kok.matches("[data-ms-ozel-select]")) {
                selectler.push(kok);
            }

            kok?.querySelectorAll?.("[data-ms-ozel-select]").forEach((select) => selectler.push(select));

            selectler.forEach((select) => {
                if (select.dataset.msOzelSelectHazir === "true") {
                    return;
                }

                const tetikleyici = select.querySelector("[data-ms-ozel-select-tetikleyici]");
                const deger = select.querySelector("[data-ms-ozel-select-deger]");
                const secenekler = select.querySelectorAll("[data-ms-ozel-select-secenek]");
                const arama = select.querySelector("[data-ms-ozel-select-arama]");
                const coklu = select.hasAttribute("data-ms-ozel-select-coklu");
                const checkboxli = select.hasAttribute("data-ms-ozel-select-checkboxli");
                const temizleButonu = select.querySelector("[data-ms-ozel-select-temizle]");
                const uygulaButonu = select.querySelector("[data-ms-ozel-select-uygula]");
                const sayac = select.querySelector("[data-ms-ozel-select-sayac]");
                const okIkonu = select.querySelector(".ms-ozel-select-ok");

                if (!tetikleyici || !deger) {
                    return;
                }

                select.dataset.msOzelSelectHazir = "true";

                secenekler.forEach((secenek, index) => {
                    if (!secenek.dataset.msOzelSelectSira) {
                        secenek.dataset.msOzelSelectSira = index.toString();
                    }
                });

                const secenekMetniAl = (secenek) => secenek.querySelector("[data-ms-ozel-select-metin]")?.textContent.trim()
                    || secenek.querySelector("span:last-child")?.textContent.trim()
                    || secenek.textContent.trim();
                const cokluVarsayilanMetin = deger.querySelector(".ms-ozel-select-placeholder")?.textContent.trim()
                    || tetikleyici.dataset.msOzelSelectVarsayilan
                    || "Seçim yapın";

                const okIkonunuGuncelle = (acik) => {
                    if (!okIkonu) {
                        return;
                    }

                    okIkonu.classList.toggle("fa-chevron-up", acik);
                    okIkonu.classList.toggle("fa-chevron-down", !acik);
                };

                const kapat = () => {
                    select.classList.remove("ms-ozel-select-acik");
                    tetikleyici.setAttribute("aria-expanded", "false");
                    okIkonunuGuncelle(false);
                };

                if (coklu) {
                    deger.querySelectorAll(".ms-ozel-select-chip-kaldir").forEach((buton) => {
                        const mevcutSecimiKaldir = (event) => {
                            event.preventDefault();
                            event.stopPropagation();

                            const chipMetni = buton.closest(".ms-ozel-select-chip")?.querySelector("span")?.textContent.trim();
                            const secenek = Array.from(secenekler).find((oge) => secenekMetniAl(oge) === chipMetni);
                            secenek?.classList.remove("ms-ozel-select-secenek-aktif");
                            cokluDegeriGuncelle();
                        };

                        buton.addEventListener("click", mevcutSecimiKaldir);
                        buton.addEventListener("keydown", (event) => {
                            if (event.key === "Enter" || event.key === " ") {
                                mevcutSecimiKaldir(event);
                            }
                        });
                    });
                }

                const cokluDegeriGuncelle = () => {
                    const aktifler = Array.from(secenekler).filter((secenek) => secenek.classList.contains("ms-ozel-select-secenek-aktif"));
                    deger.innerHTML = "";

                    aktifler.forEach((secenek) => {
                        const chip = document.createElement("span");
                        chip.className = "ms-ozel-select-chip";

                        const metin = document.createElement("span");
                        metin.textContent = secenekMetniAl(secenek);

                        const kaldir = document.createElement("span");
                        kaldir.className = "ms-ozel-select-chip-kaldir";
                        kaldir.setAttribute("role", "button");
                        kaldir.tabIndex = 0;
                        kaldir.setAttribute("aria-label", `${metin.textContent} seçimini kaldır`);
                        kaldir.textContent = "×";

                        const secimiKaldir = (event) => {
                            event.preventDefault();
                            event.stopPropagation();
                            secenek.classList.remove("ms-ozel-select-secenek-aktif");
                            cokluDegeriGuncelle();
                        };

                        kaldir.addEventListener("click", secimiKaldir);
                        kaldir.addEventListener("keydown", (event) => {
                            if (event.key === "Enter" || event.key === " ") {
                                secimiKaldir(event);
                            }
                        });

                        chip.appendChild(metin);
                        chip.appendChild(kaldir);
                        deger.appendChild(chip);
                    });

                    if (aktifler.length === 0) {
                        const placeholder = document.createElement("span");
                        placeholder.className = "ms-ozel-select-placeholder";
                        placeholder.textContent = cokluVarsayilanMetin;
                        deger.appendChild(placeholder);
                    }

                    if (sayac) {
                        sayac.hidden = aktifler.length === 0;
                        sayac.textContent = `${aktifler.length} adet seçildi`;
                    }
                    select.dispatchEvent(new CustomEvent("ms:ozel-select-degisti", {
                        bubbles: true,
                        detail: {
                            seciliAdet: aktifler.length,
                            seciliMetinler: aktifler.map(secenekMetniAl)
                        }
                    }));
                };

                const cokluSecilileriUsteTasi = () => {
                    Array.from(secenekler)
                        .sort((ilk, ikinci) => {
                            const ilkSecili = ilk.classList.contains("ms-ozel-select-secenek-aktif") ? 0 : 1;
                            const ikinciSecili = ikinci.classList.contains("ms-ozel-select-secenek-aktif") ? 0 : 1;

                            if (ilkSecili !== ikinciSecili) {
                                return ilkSecili - ikinciSecili;
                            }

                            return Number(ilk.dataset.msOzelSelectSira || 0) - Number(ikinci.dataset.msOzelSelectSira || 0);
                        })
                        .forEach((secenek) => secenek.parentElement?.appendChild(secenek));
                };

                const checkboxliDegeriGuncelle = () => {
                    const seciliCheckboxlar = Array.from(select.querySelectorAll("[data-ms-ozel-select-secenek] input[type='checkbox']:checked"));

                    if (seciliCheckboxlar.length === 0) {
                        deger.textContent = tetikleyici.dataset.msOzelSelectVarsayilan || deger.textContent || "Seçim yapın";
                        return;
                    }

                    if (!tetikleyici.dataset.msOzelSelectVarsayilan) {
                        tetikleyici.dataset.msOzelSelectVarsayilan = deger.textContent.trim();
                    }

                    deger.textContent = seciliCheckboxlar.length === 1 ? seciliCheckboxlar[0].value : `${seciliCheckboxlar.length} seçim`;
                };

                tetikleyici.addEventListener("click", () => {
                    const acik = select.classList.toggle("ms-ozel-select-acik");
                    tetikleyici.setAttribute("aria-expanded", acik.toString());
                    okIkonunuGuncelle(acik);

                    if (acik && arama) {
                        window.setTimeout(() => arama.focus(), 30);
                    }
                });

                secenekler.forEach((secenek) => {
                    secenek.addEventListener("click", (event) => {
                        if (checkboxli) {
                            const checkbox = secenek.querySelector("input[type='checkbox']");

                            if (checkbox && event.target !== checkbox) {
                                event.preventDefault();
                                checkbox.checked = !checkbox.checked;
                            }

                            checkboxliDegeriGuncelle();
                            return;
                        }

                        if (coklu) {
                            secenek.classList.toggle("ms-ozel-select-secenek-aktif");
                            cokluDegeriGuncelle();
                            return;
                        }

                        deger.textContent = secenekMetniAl(secenek);
                        secenekler.forEach((oge) => oge.classList.toggle("ms-ozel-select-secenek-aktif", oge === secenek));
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
                    if (coklu) {
                        secenekler.forEach((secenek) => {
                            secenek.classList.remove("ms-ozel-select-secenek-aktif");
                        });
                        cokluDegeriGuncelle();
                    }

                    if (checkboxli) {
                        select.querySelectorAll("[data-ms-ozel-select-secenek] input[type='checkbox']").forEach((checkbox) => {
                            checkbox.checked = false;
                        });
                    }

                    if (arama) {
                        arama.value = "";
                        secenekler.forEach((secenek) => {
                            secenek.hidden = false;
                        });
                    }

                    if (checkboxli) {
                        checkboxliDegeriGuncelle();
                    }
                });

                uygulaButonu?.addEventListener("click", () => {
                    if (coklu) {
                        cokluSecilileriUsteTasi();
                    }

                    kapat();
                });

                document.addEventListener("click", (event) => {
                    if (!select.contains(event.target)) {
                        kapat();
                    }
                });
            });

            };

            window.msOzelSelectleriBaslat = ozelSelectleriBaslat;
            ozelSelectleriBaslat(document);

            const telefonAlanlariniBaslat = (kok = document) => {
            const kapsamdakiAlanlariBul = (secici) => {
                const alanlar = [];

                if (kok?.matches?.(secici)) {
                    alanlar.push(kok);
                }

                kok?.querySelectorAll?.(secici).forEach((alan) => alanlar.push(alan));
                return alanlar;
            };

            kapsamdakiAlanlariBul("[data-ms-telefon-ulke-select]").forEach((select) => {
                if (select.dataset.msTelefonUlkeHazir === "true") {
                    return;
                }

                const tetikleyici = select.querySelector("[data-ms-telefon-ulke-tetikleyici]");
                const kod = select.querySelector("[data-ms-telefon-ulke-kod]");
                const bayrak = select.querySelector(".ms-telefon-ulke-tetikleyici .ms-telefon-bayrak");
                const arama = select.querySelector("[data-ms-telefon-ulke-arama]");
                const secenekler = select.querySelectorAll("[data-ms-telefon-ulke-secenek]");
                const ulkeKoduDegeri = select.closest(".ms-telefon-girdi")?.querySelector("[data-ms-telefon-ulke-deger]");

                if (!tetikleyici || !kod || !bayrak) {
                    return;
                }

                select.dataset.msTelefonUlkeHazir = "true";

                const kapat = () => {
                    select.classList.remove("ms-telefon-ulke-acik");
                    tetikleyici.setAttribute("aria-expanded", "false");
                };

                tetikleyici.addEventListener("click", () => {
                    const acik = select.classList.toggle("ms-telefon-ulke-acik");
                    tetikleyici.setAttribute("aria-expanded", acik.toString());

                    if (acik && arama) {
                        window.setTimeout(() => arama.focus(), 30);
                    }
                });

                secenekler.forEach((secenek) => {
                    secenek.addEventListener("click", () => {
                        kod.textContent = secenek.dataset.kod;
                        bayrak.src = secenek.dataset.bayrak;
                        bayrak.alt = secenek.dataset.ulke;

                        if (ulkeKoduDegeri) {
                            ulkeKoduDegeri.value = secenek.dataset.kod || "";
                        }

                        secenekler.forEach((oge) => {
                            oge.classList.toggle("ms-telefon-ulke-secenek-aktif", oge === secenek);
                            oge.setAttribute("aria-selected", (oge === secenek).toString());
                        });

                        kapat();
                    });
                });

                if (arama) {
                    arama.addEventListener("input", () => {
                        const aranan = arama.value.trim().toLocaleLowerCase("tr-TR");

                        secenekler.forEach((secenek) => {
                            const metin = `${secenek.dataset.ulke || ""} ${secenek.dataset.kod || ""}`.toLocaleLowerCase("tr-TR");
                            secenek.hidden = !metin.includes(aranan);
                        });
                    });
                }

                document.addEventListener("click", (event) => {
                    if (!select.contains(event.target)) {
                        kapat();
                    }
                });
            });

            kapsamdakiAlanlariBul("[data-ms-telefon-input]").forEach((input) => {
                if (input.dataset.msTelefonInputHazir === "true") {
                    return;
                }

                input.dataset.msTelefonInputHazir = "true";
                const telefonuFormatla = () => {
                    let rakamlar = input.value.replace(/\D/g, "");

                    while (rakamlar.startsWith("0")) {
                        rakamlar = rakamlar.slice(1);
                    }

                    rakamlar = rakamlar.slice(0, 10);

                    const parcalar = [
                        rakamlar.slice(0, 3),
                        rakamlar.slice(3, 6),
                        rakamlar.slice(6, 8),
                        rakamlar.slice(8, 10)
                    ].filter(Boolean);

                    input.value = parcalar.join(" ");
                };

                input.addEventListener("input", telefonuFormatla);
                input.addEventListener("paste", () => window.setTimeout(telefonuFormatla, 0));
            });
            };

            window.msTelefonAlanlariniBaslat = telefonAlanlariniBaslat;
            telefonAlanlariniBaslat(document);

            kodGirisleri.forEach((kodGiris) => {
                const inputlar = Array.from(kodGiris.querySelectorAll(".ms-kod-giris-input"));

                const kodTamamlandiysaEnterTetikle = (hedefInput = inputlar[inputlar.length - 1]) => {
                    const tumuDolu = inputlar.length > 0 && inputlar.every((input) => input.value.trim().length > 0);

                    if (!tumuDolu) {
                        return;
                    }

                    const enterOlayi = new KeyboardEvent("keydown", {
                        key: "Enter",
                        code: "Enter",
                        bubbles: true,
                        cancelable: true
                    });

                    hedefInput?.dispatchEvent(enterOlayi);

                    if (!enterOlayi.defaultPrevented) {
                        const kapsayiciEnterOlayi = new KeyboardEvent("keydown", {
                            key: "Enter",
                            code: "Enter",
                            bubbles: true,
                            cancelable: true
                        });

                        kodGiris.dispatchEvent(kapsayiciEnterOlayi);

                        if (!kapsayiciEnterOlayi.defaultPrevented) {
                            kodGiris.closest("form")?.requestSubmit?.();
                        }
                    }
                };

                const koduDagit = (deger, baslangicIndex = 0) => {
                    const rakamlar = deger.replace(/\D/g, "").slice(0, inputlar.length - baslangicIndex);

                    rakamlar.split("").forEach((rakam, index) => {
                        inputlar[baslangicIndex + index].value = rakam;
                    });

                    const sonDolanIndex = baslangicIndex + rakamlar.length - 1;
                    const sonrakiIndex = Math.min(baslangicIndex + rakamlar.length, inputlar.length - 1);
                    inputlar[sonrakiIndex]?.focus();

                    if (sonDolanIndex === inputlar.length - 1) {
                        kodTamamlandiysaEnterTetikle(inputlar[sonDolanIndex]);
                    }
                };

                inputlar.forEach((input, index) => {
                    input.addEventListener("input", () => {
                        const rakamlar = input.value.replace(/\D/g, "");

                        if (rakamlar.length > 1) {
                            input.value = "";
                            koduDagit(rakamlar, index);
                            return;
                        }

                        input.value = rakamlar;

                        if (rakamlar && inputlar[index + 1]) {
                            inputlar[index + 1].focus();
                        }

                        if (rakamlar && index === inputlar.length - 1) {
                            kodTamamlandiysaEnterTetikle(input);
                        }
                    });

                    input.addEventListener("keydown", (event) => {
                        if (event.key === "Backspace" && !input.value && inputlar[index - 1]) {
                            inputlar[index - 1].focus();
                            inputlar[index - 1].value = "";
                        }
                    });

                    input.addEventListener("paste", (event) => {
                        event.preventDefault();
                        const metin = event.clipboardData?.getData("text") || "";
                        koduDagit(metin, index);
                    });
                });
            });

            kodDetaylari.forEach((detay) => {
                const acKapat = detay.querySelector("[data-code-toggle]");
                const icerik = detay.querySelector("[data-code-content]");
                const ok = detay.querySelector(".ms-kod-ozet-ok");
                const sekmeler = detay.querySelectorAll("[data-code-tab]");
                const paneller = detay.querySelectorAll("[data-code-panel]");

                acKapat.addEventListener("click", () => {
                    const acik = acKapat.getAttribute("aria-expanded") === "true";
                    acKapat.setAttribute("aria-expanded", (!acik).toString());
                    icerik.classList.toggle("ms-kod-icerik-acik", !acik);
                    ok.classList.toggle("ms-kod-ozet-ok-acik", !acik);
                });

                sekmeler.forEach((sekme) => {
                    sekme.addEventListener("click", () => {
                        const aktifKod = sekme.dataset.codeTab;

                        sekmeler.forEach((oge) => {
                            const aktif = oge === sekme;
                            oge.setAttribute("aria-pressed", aktif.toString());
                            oge.classList.toggle("ms-kod-sekme-aktif", aktif);
                        });

                        paneller.forEach((panel) => {
                            panel.classList.toggle("ms-kod-panel-aktif", panel.dataset.codePanel === aktifKod);
                        });
                    });
                });
            });

        })();

// ProjeElementleri slider ve urun detay gorsel galeri davranislari.
(() => {
        const sliderAyar = {
            gecisSaniyesi: 2.5,
            animasyonMs: 520
        };

        const sliderlariBul = (kok = document) => {
            const sliderlar = [];

            if (kok?.matches?.("[data-ms-slider]")) {
                sliderlar.push(kok);
            }

            kok?.querySelectorAll?.("[data-ms-slider]").forEach((slider) => sliderlar.push(slider));
            return Array.from(new Set(sliderlar));
        };

        const sliderlariBaslat = (kok = document) => {
        sliderlariBul(kok).forEach((slider) => {
            const gorselAlani = slider.querySelector("[data-ms-slider-gorsel-alani]");
            const slaytlar = slider.querySelectorAll(".ms-slider-slide");
            const noktalar = slider.querySelectorAll("[data-ms-slider-index]");
            const kontroller = slider.querySelectorAll("[data-ms-slider-yon]");
            let aktifIndex = 0;
            let animasyonKaresi;
            let gecisZamani;
            let baslangicZamani = 0;
            let surukleniyor = false;
            let suruklemeBaslangicX = 0;
            let suruklemeBaslangicZamani = 0;
            let suruklemeFarki = 0;
            let tiklamaEngellenecek = false;
            let gecisYapiliyor = false;

            if (!gorselAlani || slaytlar.length === 0) {
                return;
            }

            if (slider.dataset.msSliderHazir === "true") {
                return;
            }

            slider.dataset.msSliderHazir = "true";

            const sliderGecisSaniyesi = Number.parseFloat(slider.dataset.msSliderGecisSaniyesi || "");
            const gecisSuresi = (Number.isFinite(sliderGecisSaniyesi) && sliderGecisSaniyesi > 0 ? sliderGecisSaniyesi : sliderAyar.gecisSaniyesi) * 1000;
            const animasyonSuresi = sliderAyar.animasyonMs;

            const sar = (index) => (index + slaytlar.length) % slaytlar.length;
            const suruklemeYuzdesiniSinirla = (deger) => Math.max(-100, Math.min(100, deger));
            const noktalariGuncelle = () => {
                noktalar.forEach((nokta, noktaIndex) => {
                    const aktif = noktaIndex === aktifIndex;
                    nokta.classList.toggle("ms-slider-nokta-aktif", aktif);
                    nokta.setAttribute("aria-pressed", aktif.toString());
                });
            };

            const konumlariGuncelle = (suruklemeYuzdesi = 0, yon = 0) => {
                const oncekiIndex = sar(aktifIndex - 1);
                const sonrakiIndex = sar(aktifIndex + 1);

                slaytlar.forEach((slayt, slaytIndex) => {
                    let pozisyon = 200;
                    let gorunur = slaytIndex === aktifIndex;

                    if (slaytIndex === aktifIndex) {
                        pozisyon = suruklemeYuzdesi;
                    } else if (yon < 0 && slaytIndex === sonrakiIndex) {
                        pozisyon = 100 + suruklemeYuzdesi;
                        gorunur = true;
                    } else if (yon > 0 && slaytIndex === oncekiIndex) {
                        pozisyon = -100 + suruklemeYuzdesi;
                        gorunur = true;
                    }

                    slayt.style.transform = `translate3d(${pozisyon}%, 0, 0)`;
                    slayt.classList.toggle("ms-slider-slide-gorunur", gorunur);
                    slayt.classList.toggle("ms-slider-slide-aktif", slaytIndex === aktifIndex);
                });
            };

            const goster = (index, otomatikSifirla = true) => {
                const hedefIndex = sar(index);

                if (hedefIndex === aktifIndex || gecisYapiliyor) {
                    return;
                }

                let fark = hedefIndex - aktifIndex;

                if (Math.abs(fark) > slaytlar.length / 2) {
                    fark += fark > 0 ? -slaytlar.length : slaytlar.length;
                }

                const yon = fark > 0 ? -1 : 1;
                const hedefSurukleme = fark > 0 ? -100 : 100;
                gecisYapiliyor = true;
                window.cancelAnimationFrame(animasyonKaresi);
                window.clearTimeout(gecisZamani);
                slider.style.setProperty("--ms-slider-ilerleme", "0%");
                gorselAlani.classList.add("ms-slider-gecis-hazirlaniyor");
                konumlariGuncelle(0, yon);
                gorselAlani.offsetHeight;
                gorselAlani.classList.remove("ms-slider-gecis-hazirlaniyor");

                window.requestAnimationFrame(() => {
                    konumlariGuncelle(hedefSurukleme, yon);
                });

                gecisZamani = window.setTimeout(() => {
                    aktifIndex = hedefIndex;
                    gecisYapiliyor = false;
                    gorselAlani.classList.add("ms-slider-gecis-hazirlaniyor");
                    konumlariGuncelle(0, 0);
                    noktalariGuncelle();
                    gorselAlani.offsetHeight;
                    gorselAlani.classList.remove("ms-slider-gecis-hazirlaniyor");

                    if (otomatikSifirla) {
                        otomatikBaslat();
                    }
                }, animasyonSuresi);
            };

            const surukleyerekGoster = (fark) => {
                const yon = fark > 0 ? -1 : 1;
                const hedefIndex = sar(aktifIndex + fark);
                gecisYapiliyor = true;
                window.cancelAnimationFrame(animasyonKaresi);
                window.clearTimeout(gecisZamani);
                slider.style.setProperty("--ms-slider-ilerleme", "0%");

                konumlariGuncelle(fark > 0 ? -100 : 100, yon);

                gecisZamani = window.setTimeout(() => {
                    aktifIndex = hedefIndex;
                    gecisYapiliyor = false;
                    gorselAlani.classList.add("ms-slider-gecis-hazirlaniyor");
                    konumlariGuncelle(0, 0);
                    noktalariGuncelle();
                    gorselAlani.offsetHeight;
                    gorselAlani.classList.remove("ms-slider-gecis-hazirlaniyor");

                    otomatikBaslat();
                }, animasyonSuresi);
            };

            const otomatikBaslat = () => {
                if (!slider.isConnected || document.hidden) {
                    return;
                }

                window.cancelAnimationFrame(animasyonKaresi);
                baslangicZamani = performance.now();
                slider.style.setProperty("--ms-slider-ilerleme", "0%");

                const ilerlet = (zaman) => {
                    const oran = Math.min(1, (zaman - baslangicZamani) / gecisSuresi);
                    slider.style.setProperty("--ms-slider-ilerleme", `${oran * 100}%`);

                    if (oran >= 1) {
                        goster(aktifIndex + 1);
                        return;
                    }

                    animasyonKaresi = window.requestAnimationFrame(ilerlet);
                };

                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const otomatikDurdur = () => {
                window.cancelAnimationFrame(animasyonKaresi);
            };

            kontroller.forEach((kontrol) => {
                kontrol.addEventListener("click", () => {
                    goster(aktifIndex + (kontrol.dataset.msSliderYon === "sonraki" ? 1 : -1));
                });
            });

            noktalar.forEach((nokta) => {
                nokta.addEventListener("click", () => {
                    goster(Number(nokta.dataset.msSliderIndex || 0));
                });
            });

            gorselAlani.addEventListener("dragstart", (event) => event.preventDefault());

            // iOS Safari: touch-action pan-y'ye rağmen yatay sürüklemede tarayıcı kaydırmayı
            // üstlenip pointercancel gönderebiliyor (slider hiç kaymıyordu). Yatay niyet
            // netleşince touchmove engellenir ki pointer akışı slider'da kalsın.
            let dokunmaBaslangic = null;
            gorselAlani.addEventListener("touchstart", (event) => {
                const dokunus = event.touches[0];
                dokunmaBaslangic = dokunus ? { x: dokunus.clientX, y: dokunus.clientY } : null;
            }, { passive: true });
            gorselAlani.addEventListener("touchmove", (event) => {
                if (!dokunmaBaslangic || event.touches.length !== 1 || !event.cancelable) {
                    return;
                }

                const dokunus = event.touches[0];
                const yatay = Math.abs(dokunus.clientX - dokunmaBaslangic.x);
                const dikey = Math.abs(dokunus.clientY - dokunmaBaslangic.y);

                if (yatay > dikey && yatay > 6) {
                    event.preventDefault();
                }
            }, { passive: false });

            gorselAlani.addEventListener("pointerdown", (event) => {
                if (gecisYapiliyor || (event.button !== undefined && event.button !== 0)) {
                    return;
                }

                surukleniyor = true;
                tiklamaEngellenecek = false;
                suruklemeBaslangicX = event.clientX;
                suruklemeBaslangicZamani = Date.now();
                suruklemeFarki = 0;
                gorselAlani.classList.add("ms-slider-gorsel-alani-surukleniyor");
                // 2026-07-30: setPointerCapture pointerdown'dan pointermove'a taşındı — masaüstünde
                // pointerdown'da capture, temiz tıklamada click'i child <a> yerine kapsayıcıya
                // düşürüp navigasyonu engelliyordu (dokunmada sorun yoktu). Capture artık yalnız
                // gerçek sürükleme başlayınca alınır; tek tıklama slide linkine normal ulaşır.
                otomatikDurdur();
            });

            gorselAlani.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                suruklemeFarki = event.clientX - suruklemeBaslangicX;
                const yon = suruklemeFarki < 0 ? -1 : 1;

                if (Math.abs(suruklemeFarki) > 6 && !tiklamaEngellenecek) {
                    // Gerçek sürükleme başladı: şimdi capture al (böylece kaydırma boyunca
                    // pointer olayları kapsayıcıda kalır); temiz tıklamada buraya hiç girilmez.
                    tiklamaEngellenecek = true;
                    gorselAlani.setPointerCapture?.(event.pointerId);
                }

                if (tiklamaEngellenecek) {
                    event.preventDefault();
                }

                const genislik = Math.max(1, gorselAlani.clientWidth);
                konumlariGuncelle(suruklemeYuzdesiniSinirla((suruklemeFarki / genislik) * 100), yon);
            });

            const suruklemeyiBitir = (event) => {
                if (!surukleniyor) {
                    return;
                }

                // Küçük parmak hareketi yetsin: ~%7 (en az 28px); 260ms altı fırlatmalarda 18px.
                const hizliFirlatma = Date.now() - suruklemeBaslangicZamani < 260;
                const esik = hizliFirlatma ? 18 : Math.max(28, gorselAlani.clientWidth * 0.07);
                surukleniyor = false;
                gorselAlani.classList.remove("ms-slider-gorsel-alani-surukleniyor");

                if (typeof event?.pointerId === "number" && gorselAlani.hasPointerCapture?.(event.pointerId)) {
                    gorselAlani.releasePointerCapture(event.pointerId);
                }

                if (suruklemeFarki <= -esik) {
                    surukleyerekGoster(1);
                } else if (suruklemeFarki >= esik) {
                    surukleyerekGoster(-1);
                } else {
                    konumlariGuncelle(0, suruklemeFarki < 0 ? -1 : 1);
                    window.setTimeout(() => {
                        gorselAlani.classList.add("ms-slider-gecis-hazirlaniyor");
                        konumlariGuncelle(0, 0);
                        gorselAlani.offsetHeight;
                        gorselAlani.classList.remove("ms-slider-gecis-hazirlaniyor");
                        otomatikBaslat();
                    }, animasyonSuresi);
                }

                suruklemeFarki = 0;
            };

            gorselAlani.addEventListener("pointerup", suruklemeyiBitir);
            gorselAlani.addEventListener("pointercancel", suruklemeyiBitir);
            gorselAlani.addEventListener("lostpointercapture", suruklemeyiBitir);

            gorselAlani.addEventListener("click", (event) => {
                if (tiklamaEngellenecek) {
                    event.preventDefault();
                    event.stopPropagation();
                    tiklamaEngellenecek = false;
                }
            }, true);

            document.addEventListener("visibilitychange", () => {
                if (document.hidden) {
                    otomatikDurdur();
                    return;
                }

                otomatikBaslat();
            });

            konumlariGuncelle(0, 0);
            noktalariGuncelle();
            otomatikBaslat();
        });
        };

        window.msSliderDavranislariBaslat = sliderlariBaslat;
        window.msRegisterPageModule?.("slider", sliderlariBaslat);
        sliderlariBaslat();
    })();

    (() => {
        document.querySelectorAll("[data-ms-urun-detay-resim-alani]").forEach((alan) => {
            // Urun detay sayfasi kendi sayfa scriptini kullanir; global ornek davranisi tekrar baglanmasin.
            if (alan.closest(".ms-urun-detay-sayfa")) {
                return;
            }

            const anaKapsayici = alan.querySelector("[data-ms-urun-detay-resim-surukle]");
            const track = alan.querySelector("[data-ms-urun-detay-resim-track]");
            const slaytlar = Array.from(alan.querySelectorAll("[data-ms-urun-detay-resim-slide]"));
            const thumbButonlari = Array.from(alan.querySelectorAll("[data-ms-urun-detay-resim-thumb]"));
            const yonButonlari = alan.querySelectorAll("[data-ms-urun-detay-resim-yon]");
            const modal = alan.querySelector("[data-ms-urun-detay-resim-modal]");
            const modalGorsel = alan.querySelector("[data-ms-urun-detay-resim-modal-gorsel]");
            const modalKapaticilar = alan.querySelectorAll("[data-ms-urun-detay-resim-modal-kapat]");
            const gorseller = slaytlar.map((slayt) => slayt.getAttribute("src")).filter(Boolean);
            let aktifIndex = 0;
            let surukleniyor = false;
            let baslangicX = 0;
            let suruklemeFarki = 0;
            let tiklamaEngellenecek = false;
            let gecisYapiliyor = false;
            let gecisZamani;
            let oncekiBodyOverflow = "";

            if (!anaKapsayici || !track || slaytlar.length === 0 || gorseller.length === 0) {
                return;
            }

            const sar = (index) => (index + slaytlar.length) % slaytlar.length;

            const thumbGuncelle = () => {
                thumbButonlari.forEach((buton, butonIndex) => {
                    const aktif = butonIndex === aktifIndex;
                    buton.classList.toggle("ms-urun-detay-resim-thumb-aktif", aktif);
                    buton.setAttribute("aria-pressed", aktif.toString());
                });

                thumbButonlari[aktifIndex]?.scrollIntoView({
                    behavior: "smooth",
                    block: "nearest",
                    inline: "nearest"
                });
            };

            const konumlariGuncelle = (suruklemeYuzdesi = 0, yon = 0) => {
                const oncekiIndex = sar(aktifIndex - 1);
                const sonrakiIndex = sar(aktifIndex + 1);

                slaytlar.forEach((slayt, slaytIndex) => {
                    let pozisyon = 200;
                    let gorunur = slaytIndex === aktifIndex;

                    if (slaytIndex === aktifIndex) {
                        pozisyon = suruklemeYuzdesi;
                    } else if (yon < 0 && slaytIndex === sonrakiIndex) {
                        pozisyon = 100 + suruklemeYuzdesi;
                        gorunur = true;
                    } else if (yon > 0 && slaytIndex === oncekiIndex) {
                        pozisyon = -100 + suruklemeYuzdesi;
                        gorunur = true;
                    }

                    slayt.style.transform = `translate3d(${pozisyon}%, 0, 0)`;
                    slayt.classList.toggle("ms-urun-detay-resim-ana-gorunur", gorunur);
                    slayt.classList.toggle("ms-urun-detay-resim-ana-aktif", slaytIndex === aktifIndex);
                });
            };

            const goster = (index) => {
                const hedefIndex = sar(index);

                if (hedefIndex === aktifIndex || gecisYapiliyor) {
                    return;
                }

                let fark = hedefIndex - aktifIndex;

                if (Math.abs(fark) > slaytlar.length / 2) {
                    fark += fark > 0 ? -slaytlar.length : slaytlar.length;
                }

                const yon = fark > 0 ? -1 : 1;
                const hedefSurukleme = fark > 0 ? -100 : 100;
                gecisYapiliyor = true;
                window.clearTimeout(gecisZamani);
                track.classList.add("ms-urun-detay-resim-gecis-hazirlaniyor");
                konumlariGuncelle(0, yon);
                track.offsetHeight;
                track.classList.remove("ms-urun-detay-resim-gecis-hazirlaniyor");

                window.requestAnimationFrame(() => {
                    konumlariGuncelle(hedefSurukleme, yon);
                });

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

            const surukleyerekGoster = (fark) => {
                const yon = fark > 0 ? -1 : 1;
                const hedefIndex = sar(aktifIndex + fark);
                gecisYapiliyor = true;
                window.clearTimeout(gecisZamani);
                konumlariGuncelle(fark > 0 ? -100 : 100, yon);

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

            const modalAc = () => {
                if (!modal || !modalGorsel) {
                    return;
                }

                const aktifSlayt = slaytlar[aktifIndex];
                modalGorsel.src = aktifSlayt.getAttribute("src") || "";
                modalGorsel.alt = aktifSlayt.getAttribute("alt") || "Ürün görseli";
                modal.classList.add("ms-ornek-modal-acik");
                modal.setAttribute("aria-hidden", "false");
                oncekiBodyOverflow = document.body.style.overflow;
                document.body.style.overflow = "hidden";
            };

            const modalKapat = () => {
                if (!modal) {
                    return;
                }

                modal.classList.remove("ms-ornek-modal-acik");
                modal.setAttribute("aria-hidden", "true");
                document.body.style.overflow = oncekiBodyOverflow;
            };

            thumbButonlari.forEach((buton, index) => {
                buton.addEventListener("click", () => goster(index));
            });

            yonButonlari.forEach((buton) => {
                buton.addEventListener("click", () => {
                    goster(aktifIndex + (buton.dataset.msUrunDetayResimYon === "sonraki" ? 1 : -1));
                });
            });

            modalKapaticilar.forEach((kapatici) => {
                kapatici.addEventListener("click", modalKapat);
            });

            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape" && modal?.classList.contains("ms-ornek-modal-acik")) {
                    modalKapat();
                }
            });

            anaKapsayici.addEventListener("dragstart", (event) => event.preventDefault());

            anaKapsayici.addEventListener("pointerdown", (event) => {
                if (event.button !== 0 || event.target.closest("[data-ms-urun-detay-resim-yon]") || gecisYapiliyor) {
                    return;
                }

                surukleniyor = true;
                tiklamaEngellenecek = false;
                baslangicX = event.clientX;
                suruklemeFarki = 0;
                anaKapsayici.classList.add("ms-urun-detay-resim-surukleniyor");
                anaKapsayici.setPointerCapture?.(event.pointerId);
            });

            anaKapsayici.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                suruklemeFarki = event.clientX - baslangicX;

                if (Math.abs(suruklemeFarki) > 6) {
                    tiklamaEngellenecek = true;
                    event.preventDefault();
                    konumlariGuncelle((suruklemeFarki / anaKapsayici.clientWidth) * 100, suruklemeFarki < 0 ? -1 : 1);
                }
            });

            const suruklemeyiBitir = (event) => {
                if (!surukleniyor) {
                    return;
                }

                const esik = Math.max(48, (anaKapsayici?.clientWidth || 0) * 0.1);
                surukleniyor = false;
                anaKapsayici.classList.remove("ms-urun-detay-resim-surukleniyor");

                if (suruklemeFarki <= -esik) {
                    surukleyerekGoster(1);
                } else if (suruklemeFarki >= esik) {
                    surukleyerekGoster(-1);
                } else {
                    konumlariGuncelle(0, suruklemeFarki < 0 ? -1 : 1);
                    window.setTimeout(() => {
                        track.classList.add("ms-urun-detay-resim-gecis-hazirlaniyor");
                        konumlariGuncelle(0, 0);
                        track.offsetHeight;
                        track.classList.remove("ms-urun-detay-resim-gecis-hazirlaniyor");
                    }, 300);
                }

                if (anaKapsayici.hasPointerCapture?.(event.pointerId)) {
                    anaKapsayici.releasePointerCapture(event.pointerId);
                }

                suruklemeFarki = 0;
            };

            anaKapsayici.addEventListener("pointerup", suruklemeyiBitir);
            anaKapsayici.addEventListener("pointercancel", suruklemeyiBitir);
            anaKapsayici.addEventListener("lostpointercapture", suruklemeyiBitir);
            anaKapsayici.addEventListener("click", (event) => {
                if (event.target.closest("[data-ms-urun-detay-resim-yon]")) {
                    return;
                }

                if (tiklamaEngellenecek) {
                    event.preventDefault();
                    event.stopPropagation();
                    tiklamaEngellenecek = false;
                    return;
                }

                modalAc();
            });

            konumlariGuncelle(0, 0);
            thumbGuncelle();
        });
    })();

// Urun karti favori butonlari icin global ikon ve animasyon davranisi.
(() => {
        const favoriButonlari = Array.from(document.querySelectorAll(".ms-urun-karti .ms-urun-favori"));

        favoriButonlari.forEach((buton) => {
            if (buton.dataset.msUrunFavoriHazir === "true") {
                return;
            }

            buton.dataset.msUrunFavoriHazir = "true";

            const gorselAlani = buton.closest(".ms-urun-gorsel-alani");
            let aktifKalpZamani;

            if (!gorselAlani) {
                return;
            }

            const favoriIkonuOlustur = (src, siyah = true) => {
                const ikon = document.createElement("img");
                ikon.src = src;
                ikon.alt = "";
                ikon.setAttribute("aria-hidden", "true");
                if (siyah) {
                    ikon.classList.add("ms-ikon-siyah");
                }
                return ikon;
            };

            const butonIkonunuGuncelle = (aktif) => {
                const ikon = buton.querySelector(".ms-urun-favori-ikon");

                if (ikon) {
                    ikon.src = aktif ? "/ikons/kalp.svg" : "/ikons/favorite.svg";
                }
            };

            const kucukKalpOlustur = () => {
                const kalp = document.createElement("span");
                kalp.className = "ms-urun-favori-kucuk-kalp";
                kalp.appendChild(favoriIkonuOlustur("/ikons/kalp.svg", false));
                gorselAlani.appendChild(kalp);
                kalp.addEventListener("animationend", () => kalp.remove(), { once: true });
            };

            butonIkonunuGuncelle(buton.classList.contains("ms-urun-favori-aktif"));

            buton.addEventListener("pointerdown", (event) => {
                event.stopPropagation();
            });

            buton.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();

                const aktif = buton.classList.toggle("ms-urun-favori-aktif");
                buton.setAttribute("aria-pressed", aktif.toString());
                buton.setAttribute("aria-label", aktif ? "Favorilerden çıkar" : "Favorilere ekle");
                butonIkonunuGuncelle(aktif);

                const merkezKalp = document.createElement("span");
                merkezKalp.className = aktif ? "ms-urun-favori-merkez-kalp" : "ms-urun-favori-kirik-kalp";
                merkezKalp.appendChild(favoriIkonuOlustur(aktif ? "/ikons/kalp.svg" : "/ikons/kirik-kalp.svg", false));
                gorselAlani.appendChild(merkezKalp);
                merkezKalp.addEventListener("animationend", () => merkezKalp.remove(), { once: true });

                window.clearInterval(aktifKalpZamani);

                if (aktif) {
                    kucukKalpOlustur();
                    aktifKalpZamani = window.setInterval(kucukKalpOlustur, 1150);
                }
            });
        });
    })();

// Renk önizleme (2026-08-14): renk tooltip'inde bir rengin üzerinde gezinirken kartın
// büyük görseli o rengin görseline döner; renkten/tooltip'ten çıkınca eski görsel gelir.
// SSR + dinamik kartlar için delege dinleyici; srcset düşürülür (src kazanır).
(() => {
    let onizlenenKart = null;
    let eskiSrc = "";

    const anaGorsel = (kart) => kart?.querySelector("[data-ms-urun-galeri-gorsel]");

    document.addEventListener("mouseover", (event) => {
        const secenek = event.target instanceof Element
            ? event.target.closest(".ms-urun-renk-tooltip-gorsel")
            : null;
        if (!secenek) {
            return;
        }

        const kart = secenek.closest(".ms-urun-karti");
        const gorsel = anaGorsel(kart);
        const renkImg = secenek.querySelector("img");
        // srcset'li küçük varyant değil, orijinal (liste boyu) URL: src attribute'u
        const url = renkImg?.getAttribute("src") || renkImg?.dataset.msLazySrc || "";
        if (!gorsel || !url) {
            return;
        }

        if (onizlenenKart !== kart) {
            onizlenenKart = kart;
            eskiSrc = gorsel.getAttribute("src") || "";
        }

        gorsel.removeAttribute("srcset");
        gorsel.removeAttribute("sizes");
        gorsel.removeAttribute("data-ms-lazy-srcset");
        gorsel.removeAttribute("data-ms-lazy-sizes");
        window.msUrunGorselYuklemeyeHazirla?.(gorsel, true);
        gorsel.src = url;
    });

    document.addEventListener("mouseout", (event) => {
        if (!onizlenenKart) {
            return;
        }

        const secenek = event.target instanceof Element
            ? event.target.closest(".ms-urun-renk-tooltip-gorsel")
            : null;
        if (!secenek) {
            return;
        }

        // Başka bir renk seçeneğine geçiliyorsa geri alma — yeni mouseover devralır
        const hedef = event.relatedTarget instanceof Element
            ? event.relatedTarget.closest(".ms-urun-renk-tooltip-gorsel")
            : null;
        if (hedef) {
            return;
        }

        const gorsel = anaGorsel(onizlenenKart);
        if (gorsel && eskiSrc) {
            window.msUrunGorselYuklemeyeHazirla?.(gorsel, true);
            gorsel.src = eskiSrc;
        }
        onizlenenKart = null;
        eskiSrc = "";
    });
})();

// Dinamik eklenen urun kartlari icin galeri, video, favori ve renk tooltip davranislari.
(() => {
        const favoriIkonuOlustur = (src, siyah = true) => {
            const ikon = document.createElement("img");
            ikon.src = src;
            ikon.alt = "";
            ikon.setAttribute("aria-hidden", "true");
            if (siyah) {
                ikon.classList.add("ms-ikon-siyah");
            }
            return ikon;
        };

        const galeriHazirla = (kok) => {
            const etkilesimliGaleriHedefiMi = (hedef) => hedef instanceof Element
                && Boolean(hedef.closest("button, a, input, label, [role='button'], [data-ms-urun-video], .ms-urun-favori, .ms-urun-koleksiyon, .ms-urun-renk-rozet, .ms-urun-slider-noktalari"));

            kok.querySelectorAll("[data-ms-urun-galeri]").forEach((galeri) => {
                if (galeri.dataset.msUrunGaleriHazir === "true") {
                    return;
                }

                galeri.dataset.msUrunGaleriHazir = "true";

                const gorsel = galeri.querySelector("[data-ms-urun-galeri-gorsel]");
                const resimler = (galeri.dataset.msUrunGaleriResimler || "").split("|").filter(Boolean);
                const noktalar = galeri.querySelectorAll(".ms-urun-slider-noktalari span");
                let aktifGorselIndex = 0;
                let dokunmaBaslangicX = 0;
                let dokunmaBaslangicY = 0;
                let dokunmaIslendi = false;

                if (!gorsel || !resimler.length) {
                    return;
                }

                const gorselDegistir = (index) => {
                    const hedefIndex = Math.max(0, Math.min(index, resimler.length - 1));

                    if (hedefIndex === aktifGorselIndex && gorsel.src === resimler[hedefIndex]) {
                        return;
                    }

                    aktifGorselIndex = hedefIndex;
                    gorsel.classList.add("ms-urun-gorsel-degisiyor");

                    window.setTimeout(() => {
                        // 2026-08-14: srcset dururken tarayıcı src'yi YOK SAYAR — responsive kart
                        // srcset'i canlıya çıkınca galeri "değişiyor gibi yapıp" hep ilk görseli
                        // gösteriyordu. Görsel değişiminde srcset/sizes düşürülür (src kazanır).
                        gorsel.removeAttribute("srcset");
                        gorsel.removeAttribute("sizes");
                        gorsel.removeAttribute("data-ms-lazy-srcset");
                        gorsel.removeAttribute("data-ms-lazy-sizes");
                        window.msUrunGorselYuklemeyeHazirla?.(gorsel, true);
                        gorsel.src = resimler[hedefIndex];
                        gorsel.removeAttribute("data-ms-lazy-src");
                        window.requestAnimationFrame(() => {
                            gorsel.classList.remove("ms-urun-gorsel-degisiyor");
                        });
                    }, 90);

                    noktalar.forEach((nokta, noktaIndex) => {
                        nokta.classList.toggle("ms-urun-slider-nokta-aktif", noktaIndex === hedefIndex);
                    });
                };

                galeri.addEventListener("mousemove", (event) => {
                    // Görsel hover efekti "zoom" seçiliyse galeri gezinmesi kapalı — kartta
                    // yakınlaştırma CSS'i çalışır, ikisi bir arada olmaz (2026-08-14).
                    if ((window.msKartAyarlari || {}).hoverEfekti === "zoom") {
                        return;
                    }

                    if (etkilesimliGaleriHedefiMi(event.target)) {
                        return;
                    }

                    const alan = galeri.getBoundingClientRect();
                    const oran = (event.clientX - alan.left) / alan.width;
                    const hedefIndex = Math.min(resimler.length - 1, Math.max(0, Math.floor(oran * resimler.length)));
                    gorselDegistir(hedefIndex);
                });

                galeri.addEventListener("mouseleave", () => {
                    gorselDegistir(0);
                });

                galeri.addEventListener("touchstart", (event) => {
                    if (etkilesimliGaleriHedefiMi(event.target)) {
                        dokunmaIslendi = true;
                        return;
                    }

                    const dokunma = event.touches[0];

                    if (!dokunma) {
                        return;
                    }

                    dokunmaBaslangicX = dokunma.clientX;
                    dokunmaBaslangicY = dokunma.clientY;
                    dokunmaIslendi = false;
                }, { passive: true });

                galeri.addEventListener("touchmove", (event) => {
                    const dokunma = event.touches[0];

                    if (!dokunma || dokunmaIslendi) {
                        return;
                    }

                    const farkX = dokunma.clientX - dokunmaBaslangicX;
                    const farkY = dokunma.clientY - dokunmaBaslangicY;

                    if (Math.abs(farkX) < 28 || Math.abs(farkX) < Math.abs(farkY)) {
                        return;
                    }

                    gorselDegistir(aktifGorselIndex + (farkX < 0 ? 1 : -1));
                    dokunmaIslendi = true;
                }, { passive: true });

                galeri.addEventListener("touchend", () => {
                    dokunmaIslendi = false;
                });
            });
        };

        const videoHazirla = (kok) => {
            if (window.msUrunVideoDavranisiHazirla) {
                window.msUrunVideoDavranisiHazirla(kok);
                return;
            }

            kok.querySelectorAll("[data-ms-urun-video]").forEach((videoAlani) => {
                if (videoAlani.dataset.msUrunVideoHazir === "true") {
                    return;
                }

                videoAlani.dataset.msUrunVideoHazir = "true";

                const video = videoAlani.querySelector("video");
                let kapatmaZamani;

                if (!video) {
                    return;
                }

                video.muted = true;
                video.playsInline = true;
                video.preload = "none";
                video.setAttribute("muted", "");
                video.setAttribute("playsinline", "");
                video.setAttribute("preload", "none");

                const oynat = () => {
                    window.clearTimeout(kapatmaZamani);
                    video.play().catch(() => {});
                };

                const durdur = () => {
                    kapatmaZamani = window.setTimeout(() => {
                        if (videoAlani.matches(":hover")) {
                            return;
                        }

                        video.pause();
                        video.currentTime = 0;
                    }, 90);
                };

                videoAlani.addEventListener("pointerenter", oynat);
                videoAlani.addEventListener("pointerleave", durdur);
                videoAlani.addEventListener("focusin", oynat);
                videoAlani.addEventListener("focusout", durdur);
                videoAlani.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                });
            });
        };

        const favoriHazirla = (kok) => {
            kok.querySelectorAll(".ms-urun-karti .ms-urun-favori").forEach((buton) => {
                if (buton.dataset.msUrunFavoriHazir === "true") {
                    return;
                }

                buton.dataset.msUrunFavoriHazir = "true";

                const gorselAlani = buton.closest(".ms-urun-gorsel-alani");
                let aktifKalpZamani;

                if (!gorselAlani) {
                    return;
                }

                const butonIkonunuGuncelle = (aktif) => {
                    const ikon = buton.querySelector(".ms-urun-favori-ikon");

                    if (ikon) {
                        ikon.src = aktif ? "/ikons/kalp.svg" : "/ikons/favorite.svg";
                    }
                };

                const kucukKalpOlustur = () => {
                    const kalp = document.createElement("span");
                    kalp.className = "ms-urun-favori-kucuk-kalp";
                    kalp.appendChild(favoriIkonuOlustur("/ikons/kalp.svg", false));
                    gorselAlani.appendChild(kalp);
                    kalp.addEventListener("animationend", () => kalp.remove(), { once: true });
                };

                butonIkonunuGuncelle(buton.classList.contains("ms-urun-favori-aktif"));

                buton.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();

                    const aktif = buton.classList.toggle("ms-urun-favori-aktif");
                    buton.setAttribute("aria-pressed", aktif.toString());
                    buton.setAttribute("aria-label", aktif ? "Favorilerden çıkar" : "Favorilere ekle");
                    butonIkonunuGuncelle(aktif);

                    const merkezKalp = document.createElement("span");
                    merkezKalp.className = aktif ? "ms-urun-favori-merkez-kalp" : "ms-urun-favori-kirik-kalp";
                    merkezKalp.appendChild(favoriIkonuOlustur(aktif ? "/ikons/kalp.svg" : "/ikons/kirik-kalp.svg", false));
                    gorselAlani.appendChild(merkezKalp);
                    merkezKalp.addEventListener("animationend", () => merkezKalp.remove(), { once: true });

                    window.clearInterval(aktifKalpZamani);

                    if (aktif) {
                        kucukKalpOlustur();
                        aktifKalpZamani = window.setInterval(kucukKalpOlustur, 1150);
                    }
                });
            });
        };

        const koleksiyonUrunBilgisiOlustur = (buton) => {
            const kart = buton.closest(".ms-urun-karti");
            const baslik = kart?.querySelector(".ms-urun-basligi")?.textContent?.trim();
            const gorsel = kart?.querySelector(".ms-urun-gorsel, [data-ms-urun-galeri-gorsel], img");
            const gorselYolu = gorsel?.currentSrc
                || gorsel?.getAttribute("src")
                || gorsel?.getAttribute("data-ms-lazy-src")
                || "/images/ornek-resim.jpg";
            const fiyat = kart?.querySelector(".ms-urun-fiyat, .ms-fiyat")?.textContent?.trim();
            // 2026-07-17: id = GERCEK urun kodu (API productCodes'a gider; slug turetimi
            // varchar(50) sinirini asiyordu ve koda cozulemiyordu). Slug yalniz kodsuz
            // demo kartlarin son caresi.
            const idKaynak = `${baslik || "urun"}-${gorselYolu}`;
            const id = kart?.dataset.msUrunKod
                || idKaynak
                    .toLocaleLowerCase("tr-TR")
                    .replace(/[^a-z0-9ğüşöçıİĞÜŞÖÇ]+/gi, "-")
                    .replace(/^-+|-+$/g, "")
                    .slice(0, 50)
                || `urun-${Date.now()}`;

            return {
                id,
                ad: baslik || "Ürün",
                gorsel: gorselYolu,
                meta: fiyat || "Ürün kartı"
            };
        };

        const koleksiyonHazirla = (kok) => {
            kok.querySelectorAll(".ms-urun-karti [data-ms-urun-koleksiyon]").forEach((buton) => {
                if (buton.dataset.msUrunKoleksiyonHazir === "true") {
                    return;
                }

                buton.dataset.msUrunKoleksiyonHazir = "true";

                const durumGuncelle = (aktif) => {
                    buton.classList.toggle("ms-urun-koleksiyon-aktif", aktif);
                    buton.setAttribute("aria-pressed", aktif.toString());
                    buton.setAttribute("aria-label", aktif ? "Koleksiyondan çıkar" : "Koleksiyona ekle");
                };

                durumGuncelle(buton.classList.contains("ms-urun-koleksiyon-aktif"));

                buton.addEventListener("pointerdown", (event) => {
                    event.stopPropagation();
                });

                buton.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    durumGuncelle(true);
                    const urunBilgisi = koleksiyonUrunBilgisiOlustur(buton);

                    if (typeof window.msKoleksiyonAkisBaslat === "function") {
                        window.msKoleksiyonAkisBaslat(urunBilgisi);
                        return;
                    }

                    if (typeof window.msKoleksiyonModallariBaslat === "function") {
                        window.msKoleksiyonModallariBaslat(document);
                    }
                    if (typeof window.msKoleksiyonModalAc === "function") {
                        window.msKoleksiyonModalAc(urunBilgisi);
                    }
                });
            });
        };

        const ortakRenkModalHazirla = () => {
            const modal = document.querySelector("[data-ms-urun-renk-modal]");
            const veriElemani = document.querySelector("[data-ms-urun-renk-verisi]");

            if (!modal || !veriElemani || modal.dataset.msUrunRenkModalHazir === "true") {
                return;
            }

            modal.dataset.msUrunRenkModalHazir = "true";
            const liste = modal.querySelector("[data-ms-urun-renk-modal-liste]");
            const aciklama = modal.querySelector("[data-ms-urun-renk-modal-aciklama]");
            const kapaticilar = modal.querySelectorAll("[data-ms-urun-renk-modal-kapat]");
            let renkVerisi = {};
            let aktifTetikleyici = null;
            let oncekiBodyOverflow = "";

            try {
                renkVerisi = JSON.parse(veriElemani.textContent || "{}");
            } catch {
                renkVerisi = {};
            }

            const kapat = () => {
                modal.classList.remove("ms-ornek-modal-acik");
                modal.setAttribute("aria-hidden", "true");
                modal.inert = true;
                document.body.style.overflow = oncekiBodyOverflow;
                document.documentElement.classList.remove("ms-urun-renk-tooltip-body-kilitli");
                document.body.classList.remove("ms-urun-renk-tooltip-body-kilitli");
                aktifTetikleyici?.setAttribute("aria-expanded", "false");
                aktifTetikleyici?.focus();
                aktifTetikleyici = null;
            };

            const renkleriYaz = (renkler) => {
                if (!liste) {
                    return;
                }

                const parca = document.createDocumentFragment();

                renkler.forEach((renk) => {
                    const baglanti = document.createElement("a");
                    const gorsel = document.createElement("img");
                    baglanti.className = "ms-urun-renk-tooltip-gorsel";
                    baglanti.href = renk.href || "/urun-detay";
                    baglanti.setAttribute("aria-label", `${renk.ad || "Ürün"} renk seçeneğini aç`);
                    gorsel.src = renk.gorsel || "/images/performance/urun/urun-1-90x134-v2.webp";
                    if (gorsel.src.includes("-90x134-v2.webp")) {
                        gorsel.srcset = `${gorsel.src} 1x, ${gorsel.src.replace("-90x134-v2.webp", "-180x268-v2.webp")} 2x`;
                    }
                    gorsel.alt = `${renk.ad || "Ürün"} renk seçeneği`;
                    gorsel.width = 90;
                    gorsel.height = 134;
                    gorsel.loading = "lazy";
                    gorsel.decoding = "async";
                    gorsel.decoding = "async";
                    baglanti.appendChild(gorsel);

                    if (renk.etiket) {
                        const etiket = document.createElement("span");
                        etiket.textContent = renk.etiket;
                        baglanti.appendChild(etiket);
                    }

                    parca.appendChild(baglanti);
                });

                liste.replaceChildren(parca);
            };

            const ac = (tetikleyici) => {
                const grup = tetikleyici.dataset.msUrunRenkGrubu || "varsayilan";
                const renkler = renkVerisi.renkGruplari?.[grup] || [];

                if (renkler.length === 0) {
                    return;
                }

                aktifTetikleyici = tetikleyici;
                renkleriYaz(renkler);
                if (aciklama) {
                    const urunId = tetikleyici.closest("[data-ms-urun-id]")?.dataset.msUrunId;
                    aciklama.textContent = urunId ? `${urunId} için diğer renkler` : "Ürünün diğer renklerini inceleyin.";
                }

                oncekiBodyOverflow = document.body.style.overflow;
                modal.inert = false;
                modal.classList.add("ms-ornek-modal-acik");
                modal.setAttribute("aria-hidden", "false");
                tetikleyici.setAttribute("aria-expanded", "true");
                document.body.style.overflow = "hidden";
                document.documentElement.classList.add("ms-urun-renk-tooltip-body-kilitli");
                document.body.classList.add("ms-urun-renk-tooltip-body-kilitli");
                window.setTimeout(() => modal.querySelector("[data-ms-urun-renk-modal-kapat]")?.focus(), 40);
            };

            document.addEventListener("click", (event) => {
                const tetikleyici = event.target.closest("[data-ms-urun-renk-ortak]");

                if (!tetikleyici) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();
                ac(tetikleyici);
            });

            kapaticilar.forEach((kapatici) => kapatici.addEventListener("click", kapat));
            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape" && modal.classList.contains("ms-ornek-modal-acik")) {
                    kapat();
                }
            });
        };

        const renkTooltipHazirla = (kok) => {
            ortakRenkModalHazirla();
            if (document.documentElement.dataset.msUrunRenkEscapeHazir !== "true") {
                document.documentElement.dataset.msUrunRenkEscapeHazir = "true";
                document.addEventListener("keydown", (event) => {
                    if (event.key !== "Escape") {
                        return;
                    }

                    const acikKart = document.querySelector(".ms-urun-karti.ms-urun-renk-tooltip-acik");
                    const rozet = acikKart?.querySelector(".ms-urun-renk-rozet");
                    acikKart?.classList.remove("ms-urun-renk-tooltip-acik");
                    rozet?.setAttribute("aria-expanded", "false");
                    document.documentElement.classList.remove("ms-urun-renk-tooltip-body-kilitli");
                    document.body.classList.remove("ms-urun-renk-tooltip-body-kilitli");
                    rozet?.focus();
                });
            }

            const renkTooltipDurumunuGuncelle = () => {
                const acikKartVar = Boolean(document.querySelector(".ms-urun-karti.ms-urun-renk-tooltip-acik"));
                document.documentElement.classList.toggle("ms-urun-renk-tooltip-body-kilitli", acikKartVar);
                document.body.classList.toggle("ms-urun-renk-tooltip-body-kilitli", acikKartVar);
            };

            const renkTooltipKapat = (kart) => {
                kart?.classList.remove("ms-urun-renk-tooltip-acik");
                kart?.querySelector(".ms-urun-renk-rozet")?.setAttribute("aria-expanded", "false");
                renkTooltipDurumunuGuncelle();
            };

            const renkTooltipAc = (kart) => {
                if (!kart) {
                    return;
                }

                document.querySelectorAll(".ms-urun-karti.ms-urun-renk-tooltip-acik").forEach((acikKart) => {
                    if (acikKart !== kart) {
                        acikKart.classList.remove("ms-urun-renk-tooltip-acik");
                    }
                });

                kart.classList.add("ms-urun-renk-tooltip-acik");
                kart.querySelector(".ms-urun-renk-rozet")?.setAttribute("aria-expanded", "true");
                renkTooltipDurumunuGuncelle();
            };

            const renkTooltipToggle = (kart) => {
                if (!kart) {
                    return;
                }

                if (kart.classList.contains("ms-urun-renk-tooltip-acik")) {
                    renkTooltipKapat(kart);
                } else {
                    renkTooltipAc(kart);
                }
            };

            kok.querySelectorAll(".ms-urun-renk-rozet").forEach((rozet) => {
                if (rozet.dataset.msUrunRenkHazir === "true") {
                    return;
                }

                rozet.dataset.msUrunRenkHazir = "true";

                const kart = rozet.closest(".ms-urun-karti");
                const tooltipAlani = kart?.querySelector(".ms-urun-renk-tooltip-alani");
                const kapatmaButonu = kart?.querySelector("[data-ms-renk-tooltip-kapat]");
                let kapatmaZamani;
                const mobilRenkPaneli = () => window.matchMedia("(max-width: 639px)").matches;

                if (!kart || !tooltipAlani) {
                    return;
                }

                const ac = () => {
                    window.clearTimeout(kapatmaZamani);
                    renkTooltipAc(kart);
                };

                const gecikmeliKapat = () => {
                    window.clearTimeout(kapatmaZamani);
                    kapatmaZamani = window.setTimeout(() => {
                        if (!rozet.matches(":hover") && !tooltipAlani.matches(":hover")) {
                            renkTooltipKapat(kart);
                        }
                    }, 120);
                };

                const disTiklamadaKapat = (event) => {
                    if (
                        kart.classList.contains("ms-urun-renk-tooltip-acik") &&
                        !rozet.contains(event.target) &&
                        !tooltipAlani.contains(event.target)
                    ) {
                        event.preventDefault();
                        event.stopPropagation();
                        renkTooltipKapat(kart);
                    }
                };

                rozet.addEventListener("mouseenter", () => {
                    if (!mobilRenkPaneli()) {
                        ac();
                    }
                });
                rozet.addEventListener("mouseleave", () => {
                    if (!mobilRenkPaneli()) {
                        gecikmeliKapat();
                    }
                });
                rozet.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    renkTooltipToggle(kart);
                });
                tooltipAlani.addEventListener("click", (event) => {
                    if (event.target === tooltipAlani) {
                        event.preventDefault();
                        event.stopPropagation();
                        renkTooltipKapat(kart);
                    }
                });
                tooltipAlani.addEventListener("mouseenter", () => {
                    if (!mobilRenkPaneli()) {
                        ac();
                    }
                });
                tooltipAlani.addEventListener("mouseleave", () => {
                    if (!mobilRenkPaneli()) {
                        gecikmeliKapat();
                    }
                });
                kapatmaButonu?.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    renkTooltipKapat(kart);
                });
                document.addEventListener("click", disTiklamadaKapat, true);
            });
        };

        window.msUrunKartDavranislariYenile = (kok = document) => {
            if (!kok.querySelectorAll) {
                return;
            }

            galeriHazirla(kok);
            videoHazirla(kok);
            favoriHazirla(kok);
            koleksiyonHazirla(kok);
            renkTooltipHazirla(kok);
        };

        document.querySelectorAll("[data-ms-infinite-liste]").forEach((liste) => {
            window.msUrunKartDavranislariYenile(liste);
        });

        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", () => window.msUrunKartDavranislariYenile(document), { once: true });
        } else {
            window.msUrunKartDavranislariYenile(document);
        }

        koleksiyonHazirla(document);
    })();

// ProjeElementleri urun kartlari galeri, video, favori ve renk rozet davranislari.
(() => {
        const panel = document.querySelector("[data-panel='urun-kartlari']");

        if (!panel) {
            return;
        }

        const urunGalerileri = panel.querySelectorAll("[data-ms-urun-galeri]");
        const urunVideolari = panel.querySelectorAll("[data-ms-urun-video]");
        const urunRenkRozetleri = panel.querySelectorAll(".ms-urun-renk-rozet");
        const urunFavoriButonlari = Array.from(document.querySelectorAll(".ms-urun-karti .ms-urun-favori"));
        const etkilesimliGaleriHedefiMi = (hedef) => hedef instanceof Element
            && Boolean(hedef.closest("button, a, input, label, [role='button'], [data-ms-urun-video], .ms-urun-favori, .ms-urun-koleksiyon, .ms-urun-renk-rozet, .ms-urun-slider-noktalari"));

        urunGalerileri.forEach((galeri) => {
            const gorsel = galeri.querySelector("[data-ms-urun-galeri-gorsel]");
            const resimler = (galeri.dataset.msUrunGaleriResimler || "").split("|").filter(Boolean);
            const noktalar = galeri.querySelectorAll(".ms-urun-slider-noktalari span");
            let aktifGorselIndex = 0;
            let dokunmaBaslangicX = 0;
            let dokunmaBaslangicY = 0;
            let dokunmaIslendi = false;

            if (!gorsel || resimler.length === 0) {
                return;
            }

            const gorselDegistir = (index) => {
                const hedefIndex = Math.max(0, Math.min(index, resimler.length - 1));

                if (hedefIndex === aktifGorselIndex && gorsel.src === resimler[hedefIndex]) {
                    return;
                }

                aktifGorselIndex = hedefIndex;
                gorsel.classList.add("ms-urun-gorsel-degisiyor");

                window.setTimeout(() => {
                    // 2026-08-14: srcset dururken tarayıcı src'yi yok sayar (üstteki galeri
                    // modülüyle aynı düzeltme) — değişimde srcset/sizes düşürülür.
                    gorsel.removeAttribute("srcset");
                    gorsel.removeAttribute("sizes");
                    gorsel.removeAttribute("data-ms-lazy-srcset");
                    gorsel.removeAttribute("data-ms-lazy-sizes");
                    window.msUrunGorselYuklemeyeHazirla?.(gorsel, true);
                    gorsel.src = resimler[hedefIndex];
                    window.requestAnimationFrame(() => {
                        gorsel.classList.remove("ms-urun-gorsel-degisiyor");
                    });
                }, 90);

                noktalar.forEach((nokta, noktaIndex) => {
                    nokta.classList.toggle("ms-urun-slider-nokta-aktif", noktaIndex === hedefIndex);
                });
            };

            galeri.addEventListener("mousemove", (event) => {
                // Görsel hover efekti "zoom" seçiliyse galeri gezinmesi kapalı (üstteki modülle aynı kural)
                if ((window.msKartAyarlari || {}).hoverEfekti === "zoom") {
                    return;
                }

                if (etkilesimliGaleriHedefiMi(event.target)) {
                    return;
                }

                const alan = galeri.getBoundingClientRect();
                const oran = (event.clientX - alan.left) / alan.width;
                const hedefIndex = Math.min(resimler.length - 1, Math.max(0, Math.floor(oran * resimler.length)));
                gorselDegistir(hedefIndex);
            });

            galeri.addEventListener("mouseleave", () => {
                gorselDegistir(0);
            });

            galeri.addEventListener("touchstart", (event) => {
                if (etkilesimliGaleriHedefiMi(event.target)) {
                    dokunmaIslendi = true;
                    return;
                }

                const dokunma = event.touches[0];

                if (!dokunma) {
                    return;
                }

                dokunmaBaslangicX = dokunma.clientX;
                dokunmaBaslangicY = dokunma.clientY;
                dokunmaIslendi = false;
            }, { passive: true });

            galeri.addEventListener("touchmove", (event) => {
                const dokunma = event.touches[0];

                if (!dokunma || dokunmaIslendi) {
                    return;
                }

                const farkX = dokunma.clientX - dokunmaBaslangicX;
                const farkY = dokunma.clientY - dokunmaBaslangicY;

                if (Math.abs(farkX) < 28 || Math.abs(farkX) < Math.abs(farkY)) {
                    return;
                }

                gorselDegistir(aktifGorselIndex + (farkX < 0 ? 1 : -1));
                dokunmaIslendi = true;
            }, { passive: true });

            galeri.addEventListener("touchend", () => {
                dokunmaIslendi = false;
            });
        });

        urunVideolari.forEach((videoAlani) => {
            if (window.msUrunVideoDavranisiHazirla) {
                window.msUrunVideoDavranisiHazirla(videoAlani);
                return;
            }

            const video = videoAlani.querySelector("video");
            let kapatmaZamani;
            let toparlamaZamani;

            if (!video) {
                return;
            }

            video.muted = true;
            video.playsInline = true;
            video.preload = "auto";
            video.setAttribute("muted", "");
            video.setAttribute("playsinline", "");
            video.setAttribute("preload", "auto");

            const oynat = () => {
                window.clearTimeout(kapatmaZamani);
                window.clearTimeout(toparlamaZamani);

                if (!video.paused && !video.ended) {
                    return;
                }

                video.play().catch(() => {
                    if (!videoAlani.matches(":hover")) {
                        return;
                    }

                    window.setTimeout(() => {
                        if (videoAlani.matches(":hover")) {
                            video.play().catch(() => {});
                        }
                    }, 120);
                });
            };

            const durdur = () => {
                kapatmaZamani = window.setTimeout(() => {
                    if (videoAlani.matches(":hover")) {
                        return;
                    }

                    video.pause();
                    video.currentTime = 0;
                }, 90);
            };

            const toparla = () => {
                if (!videoAlani.matches(":hover")) {
                    return;
                }

                window.clearTimeout(toparlamaZamani);
                toparlamaZamani = window.setTimeout(() => {
                    if (videoAlani.matches(":hover") && video.paused) {
                        video.play().catch(() => {});
                    }
                }, 160);
            };

            videoAlani.addEventListener("pointerenter", oynat);
            videoAlani.addEventListener("pointerleave", durdur);
            videoAlani.addEventListener("focusin", oynat);
            videoAlani.addEventListener("focusout", durdur);
            video.addEventListener("waiting", toparla);
            video.addEventListener("stalled", toparla);
            video.addEventListener("suspend", toparla);

            videoAlani.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
            });

            video.addEventListener("loadeddata", () => {
                if (videoAlani.matches(":hover")) {
                    oynat();
                }
            }, { once: true });
        });

        urunFavoriButonlari.forEach((buton) => {
            if (buton.dataset.msUrunFavoriHazir === "true") {
                return;
            }

            buton.dataset.msUrunFavoriHazir = "true";

            const gorselAlani = buton.closest(".ms-urun-gorsel-alani");
            let aktifKalpZamani;

            if (!gorselAlani) {
                return;
            }

            const favoriIkonuOlustur = (src, siyah = true) => {
                const ikon = document.createElement("img");
                ikon.src = src;
                ikon.alt = "";
                ikon.setAttribute("aria-hidden", "true");
                if (siyah) {
                    ikon.classList.add("ms-ikon-siyah");
                }
                return ikon;
            };

            const butonIkonunuGuncelle = (aktif) => {
                const ikon = buton.querySelector(".ms-urun-favori-ikon");

                if (ikon) {
                    ikon.src = aktif ? "/ikons/kalp.svg" : "/ikons/favorite.svg";
                }
            };

            const kucukKalpOlustur = () => {
                const kalp = document.createElement("span");
                kalp.className = "ms-urun-favori-kucuk-kalp";
                kalp.appendChild(favoriIkonuOlustur("/ikons/kalp.svg", false));
                gorselAlani.appendChild(kalp);
                kalp.addEventListener("animationend", () => kalp.remove(), { once: true });
            };

            butonIkonunuGuncelle(buton.classList.contains("ms-urun-favori-aktif"));

            buton.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();

                const aktif = buton.classList.toggle("ms-urun-favori-aktif");
                buton.setAttribute("aria-pressed", aktif.toString());
                buton.setAttribute("aria-label", aktif ? "Favorilerden çıkar" : "Favorilere ekle");
                butonIkonunuGuncelle(aktif);

                const merkezKalp = document.createElement("span");
                merkezKalp.className = aktif ? "ms-urun-favori-merkez-kalp" : "ms-urun-favori-kirik-kalp";
                merkezKalp.appendChild(favoriIkonuOlustur(aktif ? "/ikons/kalp.svg" : "/ikons/kirik-kalp.svg", false));
                gorselAlani.appendChild(merkezKalp);
                merkezKalp.addEventListener("animationend", () => merkezKalp.remove(), { once: true });

                window.clearInterval(aktifKalpZamani);

                if (aktif) {
                    kucukKalpOlustur();
                    aktifKalpZamani = window.setInterval(kucukKalpOlustur, 1150);
                }
            });
        });

        urunRenkRozetleri.forEach((rozet) => {
            const kart = rozet.closest(".ms-urun-karti");

            if (!kart) {
                return;
            }

            const tooltipAlani = kart.querySelector(".ms-urun-renk-tooltip-alani");
            const tooltip = kart.querySelector(".ms-urun-renk-tooltip");
            const kapatmaButonu = kart.querySelector("[data-ms-renk-tooltip-kapat]");
            let kapatmaZamani;

            const mobilRenkPaneli = () => window.matchMedia("(max-width: 639px)").matches;

            const ac = () => {
                window.clearTimeout(kapatmaZamani);
                kart.classList.add("ms-urun-renk-tooltip-acik");
            };

            const gecikmeliKapat = () => {
                window.clearTimeout(kapatmaZamani);
                kapatmaZamani = window.setTimeout(() => {
                    if (!rozet.matches(":hover") && !tooltipAlani?.matches(":hover")) {
                        kart.classList.remove("ms-urun-renk-tooltip-acik");
                    }
                }, 80);
            };

            const disTiklamadaKapat = (event) => {
                if (
                    kart.classList.contains("ms-urun-renk-tooltip-acik") &&
                    !rozet.contains(event.target) &&
                    !tooltipAlani?.contains(event.target)
                ) {
                    event.preventDefault();
                    event.stopPropagation();
                    kart.classList.remove("ms-urun-renk-tooltip-acik");
                }
            };

            rozet.addEventListener("mouseenter", () => {
                if (mobilRenkPaneli()) {
                    return;
                }

                ac();
            });

            rozet.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                kart.classList.add("ms-urun-renk-tooltip-acik");
            });

            rozet.addEventListener("mouseleave", () => {
                if (!mobilRenkPaneli()) {
                    gecikmeliKapat();
                }
            });

            tooltipAlani?.addEventListener("mouseenter", () => {
                if (!mobilRenkPaneli()) {
                    ac();
                }
            });

            tooltipAlani?.addEventListener("mouseleave", () => {
                if (!mobilRenkPaneli()) {
                    gecikmeliKapat();
                }
            });

            tooltipAlani?.addEventListener("click", (event) => {
                if (mobilRenkPaneli() && tooltip && !tooltip.contains(event.target)) {
                    kart.classList.remove("ms-urun-renk-tooltip-acik");
                }
            });

            kapatmaButonu?.addEventListener("click", () => {
                kart.classList.remove("ms-urun-renk-tooltip-acik");
            });

            document.addEventListener("click", disTiklamadaKapat, true);
        });
    })();

// ProjeElementleri story liste ve modal oynatma davranislari.
(() => {
        const storySuresi = 3000;
        const grupGecisSuresi = 280;

        document.querySelectorAll("[data-ms-story]").forEach((storyAlani) => {
            const tetikleyiciler = Array.from(storyAlani.querySelectorAll("[data-ms-story-ac]"));
            const storyVeri = storyAlani.querySelector("[data-ms-story-veri]");
            const modal = storyAlani.querySelector("[data-ms-story-modal]");
            const progressListesi = storyAlani.querySelector("[data-ms-story-progress-listesi]");
            const modalGorsel = storyAlani.querySelector("[data-ms-story-modal-gorsel]");
            const modalVideo = storyAlani.querySelector("[data-ms-story-modal-video]");
            const avatar = storyAlani.querySelector("[data-ms-story-avatar]");
            const modalBaslik = storyAlani.querySelector("[data-ms-story-modal-baslik]");
            const altBaslik = storyAlani.querySelector("[data-ms-story-alt-baslik]");
            const aksiyon = storyAlani.querySelector("[data-ms-story-aksiyon]");
            const cerceve = storyAlani.querySelector(".ms-story-cerceve");
            const ortaOynat = storyAlani.querySelector("[data-ms-story-orta-oynat]");
            const ortaIkon = storyAlani.querySelector("[data-ms-story-orta-ikon]");
            const yonButonlari = storyAlani.querySelectorAll("[data-ms-story-yon]");
            const kapatmaButonlari = storyAlani.querySelectorAll("[data-ms-story-kapat]");
            const storyGruplari = storyVeri ? JSON.parse(storyVeri.textContent || "{}") : {};
            const grupSirasi = tetikleyiciler
                .map((buton) => buton.dataset.msStoryGrup)
                .filter((grupAdi, index, liste) => grupAdi && Array.isArray(storyGruplari[grupAdi]) && storyGruplari[grupAdi].length && liste.indexOf(grupAdi) === index);
            let hikayeler = [];
            let aktifGrupIndex = 0;
            let aktifIndex = 0;
            let aktifStorySuresi = storySuresi;
            let baslangic = 0;
            let gecenSure = 0;
            let animasyonKaresi = 0;
            let grupGecisZamanlayici = 0;
            let duraklatildi = false;
            let modalAcik = false;
            let storyBasili = false;
            let storyBasiliDuraklatildi = false;
            let storyBasiliOnceDuraklatildi = false;
            let storyBasiliZamanlayici = 0;
            const mobilStoryEslesmesi = window.matchMedia("(max-width: 639px)");

            if (!modal || !cerceve || !modalGorsel || !modalVideo || !grupSirasi.length) {
                return;
            }

            const rgbMetni = (rgb) => `${rgb.r}, ${rgb.g}, ${rgb.b}`;
            const hexMetni = (rgb) => `#${[rgb.r, rgb.g, rgb.b].map((deger) => deger.toString(16).padStart(2, "0")).join("")}`;
            const renkKanaliniSinirla = (deger) => Math.max(0, Math.min(255, deger));

            const storyRenginiUygula = (kart, rgb) => {
                kart.style.setProperty("--ms-story-renk", hexMetni(rgb));
                kart.style.setProperty("--ms-story-renk-rgb", rgbMetni(rgb));
            };

            const storyRenginiTara = (kart) => {
                const gorsel = kart.querySelector("img");

                if (!gorsel) {
                    return;
                }

                const tara = () => {
                    const canvas = document.createElement("canvas");
                    const boyut = 48;
                    canvas.width = boyut;
                    canvas.height = boyut;
                    const context = canvas.getContext("2d", { willReadFrequently: true });

                    if (!context) {
                        return;
                    }

                    context.drawImage(gorsel, 0, 0, boyut, boyut);
                    const pikseller = context.getImageData(0, 0, boyut, boyut).data;
                    const renkler = new Map();

                    for (let index = 0; index < pikseller.length; index += 4) {
                        const alpha = pikseller[index + 3];
                        const r = pikseller[index];
                        const g = pikseller[index + 1];
                        const b = pikseller[index + 2];
                        const parlaklik = (r + g + b) / 3;
                        const enYuksek = Math.max(r, g, b);
                        const enDusuk = Math.min(r, g, b);
                        const doygunluk = enYuksek - enDusuk;

                        if (alpha < 80 || parlaklik > 245 || parlaklik < 70 || doygunluk < 45) {
                            continue;
                        }

                        const anahtar = [
                            renkKanaliniSinirla(Math.round(r / 24) * 24),
                            renkKanaliniSinirla(Math.round(g / 24) * 24),
                            renkKanaliniSinirla(Math.round(b / 24) * 24)
                        ].join(",");
                        renkler.set(anahtar, (renkler.get(anahtar) || 0) + 1 + doygunluk / 80);
                    }

                    const baskin = [...renkler.entries()].sort((a, b) => b[1] - a[1])[0];

                    if (!baskin) {
                        return;
                    }

                    const [r, g, b] = baskin[0].split(",").map(Number);
                    storyRenginiUygula(kart, { r, g, b });
                };

                if (gorsel.complete) {
                    tara();
                } else {
                    gorsel.addEventListener("load", tara, { once: true });
                }
            };

            tetikleyiciler.forEach(storyRenginiTara);

            const ikonlariGuncelle = () => {
                const oynatPath = '<path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-1.427 1.529-2.33 2.778-1.64l11.16 6.347c1.255.714 1.255 2.521 0 3.235L8.028 19.942c-1.249.71-2.778-.192-2.778-1.62V5.653Z" />';
                ortaIkon.innerHTML = oynatPath;
                cerceve.classList.toggle("ms-story-duraklatildi", duraklatildi);
                ortaOynat.setAttribute("aria-hidden", (!duraklatildi).toString());
            };

            const progressleriOlustur = () => {
                progressListesi.innerHTML = "";
                hikayeler.forEach(() => {
                    const cubuk = document.createElement("span");
                    cubuk.className = "ms-story-progress";
                    cubuk.innerHTML = "<span></span>";
                    progressListesi.appendChild(cubuk);
                });
            };

            const progressleriGuncelle = (oran = 0) => {
                progressListesi.querySelectorAll(".ms-story-progress").forEach((progress, index) => {
                    const doluAlan = progress.querySelector("span");
                    progress.classList.toggle("ms-story-progress-aktif", index === aktifIndex);
                    progress.classList.toggle("ms-story-progress-tamamlandi", index < aktifIndex);
                    doluAlan.style.width = index < aktifIndex ? "100%" : index === aktifIndex ? `${oran * 100}%` : "0%";
                });
            };

            const hikayeSuresiniOku = (hikaye) => {
                if (hikaye?.sureMs && Number.isFinite(Number(hikaye.sureMs))) {
                    return Math.max(1000, Number(hikaye.sureMs));
                }

                if (hikaye?.sure && Number.isFinite(Number(hikaye.sure))) {
                    return Math.max(1000, Number(hikaye.sure) * 1000);
                }

                if (hikaye?.tip === "video" && Number.isFinite(modalVideo.duration) && modalVideo.duration > 0) {
                    return Math.max(1000, modalVideo.duration * 1000);
                }

                return storySuresi;
            };

            const grupGecisiniOynat = () => {
                window.clearTimeout(grupGecisZamanlayici);
                cerceve.classList.remove("ms-story-grup-gecis");
                void cerceve.offsetWidth;
                cerceve.classList.add("ms-story-grup-gecis");
                grupGecisZamanlayici = window.setTimeout(() => {
                    cerceve.classList.remove("ms-story-grup-gecis");
                }, grupGecisSuresi);
            };

            const ilerlet = (zaman) => {
                if (!modalAcik || duraklatildi) {
                    return;
                }

                if (!baslangic) {
                    baslangic = zaman - gecenSure;
                }

                gecenSure = zaman - baslangic;
                const oran = Math.min(1, gecenSure / aktifStorySuresi);
                progressleriGuncelle(oran);

                if (oran >= 1) {
                    goster(aktifIndex + 1);
                    return;
                }

                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const aktifVideoMu = () => !modalVideo.classList.contains("ms-gizli");

            const grupYukle = (grupIndex, storyIndex = 0, animasyonlu = false) => {
                aktifGrupIndex = (grupIndex + grupSirasi.length) % grupSirasi.length;
                hikayeler = storyGruplari[grupSirasi[aktifGrupIndex]] || [];

                if (!hikayeler.length) {
                    return;
                }

                progressleriOlustur();

                if (animasyonlu) {
                    grupGecisiniOynat();
                }

                goster(storyIndex);
            };

            const grupDegistir = (yon) => {
                const hedefGrupIndex = aktifGrupIndex + yon;
                const hedefGrupAdi = grupSirasi[(hedefGrupIndex + grupSirasi.length) % grupSirasi.length];
                const hedefHikayeler = storyGruplari[hedefGrupAdi] || [];
                const hedefStoryIndex = yon > 0 ? 0 : Math.max(0, hedefHikayeler.length - 1);
                grupYukle(hedefGrupIndex, hedefStoryIndex, true);
            };

            const otomatikBaslat = () => {
                window.cancelAnimationFrame(animasyonKaresi);
                baslangic = 0;
                gecenSure = 0;
                duraklatildi = false;
                ikonlariGuncelle();

                if (aktifVideoMu()) {
                    modalVideo.currentTime = 0;
                    modalVideo.play().catch(() => {});
                }

                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const goster = (index) => {
                if (index >= hikayeler.length) {
                    grupDegistir(1);
                    return;
                }

                if (index < 0) {
                    grupDegistir(-1);
                    return;
                }

                aktifIndex = index;
                const hikaye = hikayeler[aktifIndex];
                const videoMu = hikaye.tip === "video";

                aktifStorySuresi = hikayeSuresiniOku(hikaye);
                modalGorsel.classList.toggle("ms-gizli", videoMu);
                modalVideo.classList.toggle("ms-gizli", !videoMu);
                modalVideo.pause();
                modalVideo.onloadedmetadata = null;

                if (videoMu) {
                    modalVideo.src = hikaye.url;
                    modalVideo.setAttribute("aria-label", `${hikaye.baslik} story videosu`);
                    modalGorsel.removeAttribute("src");
                    modalGorsel.alt = "";
                    modalVideo.onloadedmetadata = () => {
                        aktifStorySuresi = hikayeSuresiniOku(hikaye);
                        baslangic = 0;
                        gecenSure = 0;
                        progressleriGuncelle(0);

                        if (!duraklatildi && modalAcik) {
                            window.cancelAnimationFrame(animasyonKaresi);
                            animasyonKaresi = window.requestAnimationFrame(ilerlet);
                        }
                    };
                } else {
                    modalGorsel.src = hikaye.url;
                    modalGorsel.alt = `${hikaye.baslik} story görseli`;
                    modalVideo.removeAttribute("src");
                    modalVideo.load();
                }

                avatar.src = hikaye.avatar;
                avatar.alt = hikaye.baslik;
                modalBaslik.textContent = hikaye.baslik;
                altBaslik.textContent = hikaye.baslik;
                aksiyon.textContent = hikaye.aksiyon;
                progressleriGuncelle(0);
                otomatikBaslat();
            };

            const ac = (grupAdi) => {
                const grupIndex = grupSirasi.indexOf(grupAdi);

                if (grupIndex < 0) {
                    return;
                }

                modalAcik = true;
                modal.classList.remove("ms-gizli");
                modal.setAttribute("aria-hidden", "false");
                document.body.classList.add("ms-story-modal-acik");
                grupYukle(grupIndex, 0, false);
            };

            const kapat = () => {
                modalAcik = false;
                window.cancelAnimationFrame(animasyonKaresi);
                modal.classList.add("ms-gizli");
                modal.setAttribute("aria-hidden", "true");
                modalVideo.pause();
                modalVideo.removeAttribute("src");
                modalVideo.load();
                modalGorsel.removeAttribute("src");
                modalGorsel.alt = "";
                window.clearTimeout(grupGecisZamanlayici);
                cerceve.classList.remove("ms-story-grup-gecis");
                avatar?.removeAttribute("src");
                if (avatar) {
                    avatar.alt = "";
                }
                document.body.classList.remove("ms-story-modal-acik");
            };

            const duraklatToggle = () => {
                duraklatildi = !duraklatildi;
                ikonlariGuncelle();

                if (duraklatildi) {
                    modalVideo.pause();
                    return;
                }

                if (aktifVideoMu()) {
                    modalVideo.play().catch(() => {});
                }

                baslangic = 0;
                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const duraklat = () => {
                if (duraklatildi) {
                    return;
                }

                duraklatildi = true;
                modalVideo.pause();
                ikonlariGuncelle();
            };

            const oynat = () => {
                if (!duraklatildi) {
                    return;
                }

                duraklatildi = false;
                ikonlariGuncelle();

                if (aktifVideoMu()) {
                    modalVideo.play().catch(() => {});
                }

                baslangic = 0;
                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const storyEtkilesimHedefiMi = (event) => event.target.closest("[data-ms-story-kapat], [data-ms-story-yon], [data-ms-story-orta-oynat], [data-ms-story-aksiyon]");

            const mobilStoryTikla = (event) => {
                const alan = cerceve.getBoundingClientRect();
                const x = event.clientX - alan.left;
                const bolge = alan.width / 3;

                if (x < bolge) {
                    goster(aktifIndex - 1);
                } else if (x > bolge * 2) {
                    goster(aktifIndex + 1);
                } else {
                    duraklatToggle();
                }
            };

            cerceve.addEventListener("pointerdown", (event) => {
                if (!modalAcik || !mobilStoryEslesmesi.matches || storyEtkilesimHedefiMi(event)) {
                    return;
                }

                storyBasili = true;
                storyBasiliDuraklatildi = false;
                storyBasiliOnceDuraklatildi = duraklatildi;
                window.clearTimeout(storyBasiliZamanlayici);
                storyBasiliZamanlayici = window.setTimeout(() => {
                    if (!storyBasili || storyBasiliOnceDuraklatildi) {
                        return;
                    }

                    storyBasiliDuraklatildi = true;
                    duraklat();
                }, 220);
            });

            cerceve.addEventListener("pointerup", (event) => {
                if (!modalAcik || !mobilStoryEslesmesi.matches || storyEtkilesimHedefiMi(event)) {
                    return;
                }

                window.clearTimeout(storyBasiliZamanlayici);
                storyBasili = false;

                if (storyBasiliDuraklatildi) {
                    oynat();
                    return;
                }

                mobilStoryTikla(event);
            });

            cerceve.addEventListener("pointercancel", () => {
                window.clearTimeout(storyBasiliZamanlayici);
                storyBasili = false;

                if (storyBasiliDuraklatildi) {
                    oynat();
                }
            });

            tetikleyiciler.forEach((buton) => {
                buton.addEventListener("click", () => ac(buton.dataset.msStoryGrup));
            });

            yonButonlari.forEach((buton) => {
                buton.addEventListener("click", () => {
                    goster(aktifIndex + (buton.dataset.msStoryYon === "sonraki" ? 1 : -1));
                });
            });

            kapatmaButonlari.forEach((buton) => buton.addEventListener("click", kapat));
            modalGorsel.addEventListener("click", () => {
                if (!mobilStoryEslesmesi.matches) {
                    duraklat();
                }
            });
            modalVideo.addEventListener("click", () => {
                if (!mobilStoryEslesmesi.matches) {
                    duraklat();
                }
            });
            ortaOynat.addEventListener("click", oynat);
            modalVideo.addEventListener("ended", () => {
                if (modalAcik && aktifVideoMu()) {
                    goster(aktifIndex + 1);
                }
            });

            document.addEventListener("keydown", (event) => {
                if (!modalAcik) {
                    return;
                }

                if (event.key === "Escape") {
                    kapat();
                } else if (event.key === "ArrowRight") {
                    goster(aktifIndex + 1);
                } else if (event.key === "ArrowLeft") {
                    goster(aktifIndex - 1);
                } else if (event.key === " ") {
                    event.preventDefault();
                    duraklatToggle();
                }
            });
        });
    })();

// Ana navigasyon ortak davranislari (Razor disina tasindi).
// Desktop navigasyonda aşağı kaydırırken yalnızca üst barı sabit bırakır, yukarı kaydırmada tüm alanı geri getirir.
(() => {
    const wrapperlar = document.querySelectorAll(".ms-ana-navigasyon-wrapper");

    if (wrapperlar.length === 0) {
        return;
    }

    const desktopMedya = window.matchMedia("(min-width: 1024px)");
    const kompaktBaslamaMesafesi = 120;
    const geriAcmaMesafesi = 32;
    const classDegisimBekleme = 240;
    let sonScrollY = window.scrollY || window.pageYOffset || 0;
    let guncellemePlanlandi = false;
    let sonClassDegisimZamani = 0;
    let asagiMesafe = 0;
    let yukariMesafe = 0;

    const zamanDamgasiAl = () => (window.performance?.now ? window.performance.now() : Date.now());

    const navigasyonYuksekliginiYaz = () => {
        const yukseklik = Math.ceil(Math.max(...Array.from(wrapperlar).map((wrapper) => wrapper.getBoundingClientRect().height), 0));
        document.documentElement.style.setProperty("--ms-ana-navigasyon-aktif-yukseklik", `${yukseklik}px`);
    };

    const panelAcikMi = (wrapper) => wrapper.querySelector(
        ".ms-ana-navigasyon-arama-panel-acik, .ms-ana-navigasyon-sepet-acik, .ms-ana-navigasyon-giris-acik"
    );

    const kompaktDurumuAyarla = (kompakt) => {
        let degisti = false;

        wrapperlar.forEach((wrapper) => {
            const uygulanacak = kompakt && !panelAcikMi(wrapper);
            if (wrapper.classList.contains("ms-ana-navigasyon-kompakt") !== uygulanacak) {
                degisti = true;
            }

            wrapper.classList.toggle("ms-ana-navigasyon-kompakt", uygulanacak);
        });

        if (degisti) {
            sonClassDegisimZamani = zamanDamgasiAl();
            window.requestAnimationFrame(navigasyonYuksekliginiYaz);
        }
    };

    const navigasyonuGuncelle = () => {
        guncellemePlanlandi = false;

        if (!desktopMedya.matches) {
            kompaktDurumuAyarla(false);
            asagiMesafe = 0;
            yukariMesafe = 0;
            sonScrollY = window.scrollY || window.pageYOffset || 0;
            return;
        }

        const simdikiScrollY = Math.max(0, window.scrollY || window.pageYOffset || 0);
        const fark = simdikiScrollY - sonScrollY;
        const simdi = zamanDamgasiAl();

        if (simdikiScrollY < 24) {
            kompaktDurumuAyarla(false);
            asagiMesafe = 0;
            yukariMesafe = 0;
            sonScrollY = simdikiScrollY;
            return;
        }

        if (simdi - sonClassDegisimZamani < classDegisimBekleme) {
            sonScrollY = simdikiScrollY;
            return;
        }

        if (fark > 0) {
            const kompaktMi = Array.from(wrapperlar).some((wrapper) => wrapper.classList.contains("ms-ana-navigasyon-kompakt"));
            asagiMesafe += fark;
            yukariMesafe = 0;

            if (kompaktMi || asagiMesafe >= kompaktBaslamaMesafesi) {
                kompaktDurumuAyarla(true);
                asagiMesafe = 0;
            }
        } else if (fark < 0) {
            yukariMesafe += Math.abs(fark);
            asagiMesafe = 0;

            if (yukariMesafe >= geriAcmaMesafesi) {
                kompaktDurumuAyarla(false);
                yukariMesafe = 0;
            }
        }

        sonScrollY = simdikiScrollY;
    };

    const navigasyonuPlanla = () => {
        if (guncellemePlanlandi) {
            return;
        }

        guncellemePlanlandi = true;
        window.requestAnimationFrame(navigasyonuGuncelle);
    };

    window.addEventListener("scroll", navigasyonuPlanla, { passive: true });
    window.addEventListener("resize", () => {
        navigasyonYuksekliginiYaz();
        navigasyonuPlanla();
    });
    desktopMedya.addEventListener?.("change", () => {
        navigasyonYuksekliginiYaz();
        navigasyonuPlanla();
    });
    navigasyonYuksekliginiYaz();
    navigasyonuGuncelle();
})();

// Mobil kategori menüsü aç/kapat, üst sekme ve sol kategori davranışları.
(() => {
    const panel = document.querySelector("[data-ms-mobil-menu]");
    const acButonu = document.querySelector("[data-ms-mobil-menu-ac]");

    if (!panel || !acButonu) {
        return;
    }

    const mobilMenuBaslat = () => {
        const sablon = panel.querySelector("[data-ms-mobil-menu-sablon]");

        if (sablon instanceof HTMLTemplateElement) {
            panel.appendChild(sablon.content.cloneNode(true));
            sablon.remove();
        }

    const mobilMenuKaydirmaAlani = document.querySelector("[data-ms-mobil-menu-kaydirma-alani]");
    const mobilAramaAlani = document.querySelector(".ms-ana-navigasyon-arama");
    const mobilAramaUstAlani = mobilAramaAlani?.closest(".ms-ana-navigasyon-ust");
    const anaSayfaYollari = ["/", "/home", "/home/index"];
    const anaSayfaMobilNavigasyonAktif = Boolean(document.querySelector(".ms-ana-sayfa"))
        || anaSayfaYollari.includes(window.location.pathname.toLowerCase());
    const kapatButonlari = panel.querySelectorAll("[data-ms-mobil-menu-kapat]");
    const anaSekmeler = panel.querySelectorAll("[data-ms-mobil-ana-sekme]");
    const yanSekmeler = panel.querySelectorAll("[data-ms-mobil-yan-sekme]");
    const yanGruplar = panel.querySelectorAll("[data-ms-mobil-yan-grup]");
    const paneller = panel.querySelectorAll("[data-ms-mobil-panel]");
    const kampanyaAlani = panel.querySelector(".ms-ana-navigasyon-mobil-kampanya");
    const kampanyaAcButonu = panel.querySelector("[data-ms-mobil-kampanya-ac]");
    const kampanyaListesi = panel.querySelector("[data-ms-mobil-kampanya-listesi]");
    const kampanyaKontrolleri = panel.querySelectorAll("[data-ms-mobil-kampanya-kaydir]");
    let sonOdaklananEleman = null;

    const yanSekmeAc = (hedef) => {
        yanSekmeler.forEach((sekme) => {
            const aktif = sekme.dataset.msMobilYanSekme === hedef;
            sekme.classList.toggle("ms-ana-navigasyon-mobil-yan-sekme-aktif", aktif);
            sekme.setAttribute("aria-pressed", aktif ? "true" : "false");
        });

        paneller.forEach((mobilPanel) => {
            mobilPanel.hidden = mobilPanel.dataset.msMobilPanel !== hedef;
        });
    };

    const anaSekmeAc = (hedef) => {
        anaSekmeler.forEach((sekme) => {
            const aktif = sekme.dataset.msMobilAnaSekme === hedef;
            sekme.classList.toggle("ms-ana-navigasyon-mobil-ana-sekme-aktif", aktif);
            sekme.setAttribute("aria-pressed", aktif ? "true" : "false");
        });

        yanGruplar.forEach((grup) => {
            grup.hidden = grup.dataset.msMobilYanGrup !== hedef;
        });

        const ilkYanSekme = panel.querySelector(`[data-ms-mobil-yan-grup="${hedef}"] [data-ms-mobil-yan-sekme]`);
        if (ilkYanSekme) {
            yanSekmeAc(ilkYanSekme.dataset.msMobilYanSekme);
        }
    };

    const panelAc = () => {
        sonOdaklananEleman = document.activeElement;
        panel.inert = false;
        panel.classList.add("ms-ana-navigasyon-mobil-panel-acik");
        panel.setAttribute("aria-hidden", "false");
        acButonu.setAttribute("aria-expanded", "true");
        document.body.style.overflow = "hidden";
        window.setTimeout(() => panel.querySelector("[data-ms-mobil-menu-kapat]")?.focus(), 40);
    };

    const panelKapat = () => {
        panel.classList.remove("ms-ana-navigasyon-mobil-panel-acik");
        panel.setAttribute("aria-hidden", "true");
        panel.inert = true;
        acButonu.setAttribute("aria-expanded", "false");
        kampanyaKapat();
        document.body.style.overflow = "";
        sonOdaklananEleman?.focus?.();
    };

    acButonu.addEventListener("click", panelAc);
    kapatButonlari.forEach((buton) => buton.addEventListener("click", panelKapat));
    anaSekmeler.forEach((sekme) => sekme.addEventListener("click", () => {
        // 2026-09-01: alt kategorisi olmayan kök (data-ms-mobil-ana-url dolu) panel açmaz,
        // doğrudan ürün listeleme sayfasına gider (örn. Etiketin Yarısı → /etiketin-yarisi).
        const dogrudanUrl = sekme.dataset.msMobilAnaUrl;
        if (dogrudanUrl) {
            window.location.href = dogrudanUrl;
            return;
        }
        anaSekmeAc(sekme.dataset.msMobilAnaSekme);
    }));
    yanSekmeler.forEach((sekme) => sekme.addEventListener("click", () => yanSekmeAc(sekme.dataset.msMobilYanSekme)));

    const kampanyaKapat = () => {
        kampanyaAlani?.classList.remove("ms-ana-navigasyon-mobil-kampanya-acik");
        kampanyaAcButonu?.setAttribute("aria-expanded", "false");
    };

    const kampanyaToggle = () => {
        if (!kampanyaAlani || !kampanyaAcButonu) {
            return;
        }

        const acik = kampanyaAlani.classList.toggle("ms-ana-navigasyon-mobil-kampanya-acik");
        kampanyaAcButonu.setAttribute("aria-expanded", acik ? "true" : "false");

        if (acik) {
            requestAnimationFrame(kampanyaKaydirmaDurumuGuncelle);
        }
    };

    kampanyaAcButonu?.addEventListener("click", (event) => {
        event.stopPropagation();
        kampanyaToggle();
    });

    const kampanyaKaydirmaDurumuGuncelle = () => {
        if (!kampanyaListesi || kampanyaKontrolleri.length === 0) {
            return;
        }

        const kaydirilabilir = kampanyaListesi.scrollWidth > kampanyaListesi.clientWidth + 1;
        const enSolda = kampanyaListesi.scrollLeft <= 1;
        const enSagda = kampanyaListesi.scrollLeft + kampanyaListesi.clientWidth >= kampanyaListesi.scrollWidth - 1;

        kampanyaListesi.classList.toggle("ms-magaza-mega-kampanya-listesi-kaydirilabilir", kaydirilabilir);
        kampanyaKontrolleri.forEach((kontrol) => {
            const solKontrol = kontrol.dataset.msMobilKampanyaKaydir === "sol";
            kontrol.classList.toggle("ms-magaza-mega-kampanya-kontrol-aktif", kaydirilabilir);
            kontrol.disabled = !kaydirilabilir || (solKontrol && enSolda) || (!solKontrol && enSagda);
        });
    };

    kampanyaKontrolleri.forEach((kontrol) => {
        kontrol.addEventListener("click", () => {
            if (!kampanyaListesi) {
                return;
            }

            const yon = kontrol.dataset.msMobilKampanyaKaydir === "sol" ? -1 : 1;
            kampanyaListesi.scrollTo({
                left: kampanyaListesi.scrollLeft + (kampanyaListesi.clientWidth * 0.72 * yon),
                behavior: "smooth"
            });
        });
    });

    if (kampanyaListesi) {
        let surukleniyor = false;
        let baslangicX = 0;
        let baslangicScroll = 0;
        let tiklamayiEngelle = false;

        kampanyaListesi.addEventListener("dragstart", (event) => event.preventDefault());
        kampanyaListesi.addEventListener("pointerdown", (event) => {
            if (event.button !== 0) {
                return;
            }

            surukleniyor = true;
            baslangicX = event.clientX;
            baslangicScroll = kampanyaListesi.scrollLeft;
            tiklamayiEngelle = false;
            kampanyaListesi.classList.add("ms-magaza-mega-kampanya-listesi-surukleniyor");
            kampanyaListesi.setPointerCapture?.(event.pointerId);
        });

        kampanyaListesi.addEventListener("pointermove", (event) => {
            if (!surukleniyor) {
                return;
            }

            const fark = event.clientX - baslangicX;
            if (Math.abs(fark) > 5) {
                tiklamayiEngelle = true;
            }

            kampanyaListesi.scrollLeft = baslangicScroll - fark;
            kampanyaKaydirmaDurumuGuncelle();
        });

        const suruklemeyiBitir = (event) => {
            if (!surukleniyor) {
                return;
            }

            surukleniyor = false;
            kampanyaListesi.classList.remove("ms-magaza-mega-kampanya-listesi-surukleniyor");

            if (kampanyaListesi.hasPointerCapture?.(event.pointerId)) {
                kampanyaListesi.releasePointerCapture(event.pointerId);
            }
        };

        kampanyaListesi.addEventListener("pointerup", suruklemeyiBitir);
        kampanyaListesi.addEventListener("pointercancel", suruklemeyiBitir);
        kampanyaListesi.addEventListener("scroll", kampanyaKaydirmaDurumuGuncelle, { passive: true });
        kampanyaListesi.addEventListener("click", (event) => {
            if (!tiklamayiEngelle) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            tiklamayiEngelle = false;
        }, true);
        window.addEventListener("resize", kampanyaKaydirmaDurumuGuncelle);
        requestAnimationFrame(kampanyaKaydirmaDurumuGuncelle);
    }

    if (mobilMenuKaydirmaAlani || mobilAramaAlani) {
        let sonKaydirmaYonu = 0;
        let sonKaydirmaZamani = 0;
        let dokunmaBaslangicY = 0;
        let mobilMenuGizli = false;
        let mobilAramaGizli = false;
        let guncellemePlanlandi = false;

        const mobilNavigasyonMu = () => window.matchMedia("(max-width: 1023px)").matches;
        const zamanDamgasiAl = () => (window.performance?.now ? window.performance.now() : Date.now());

        const mobilMenuGorunumunuAyarla = (gizli) => {
            if (!mobilNavigasyonMu() || !anaSayfaMobilNavigasyonAktif || !mobilMenuKaydirmaAlani) {
                mobilMenuKaydirmaAlani?.classList.remove("ms-magaza-mobil-menu-kaydirma-gizli");
                mobilMenuGizli = false;
                return;
            }

            if (mobilMenuGizli === gizli) {
                return;
            }

            mobilMenuGizli = gizli;
            mobilMenuKaydirmaAlani.classList.toggle("ms-magaza-mobil-menu-kaydirma-gizli", gizli);
        };

        const mobilAramaGorunumunuAyarla = (gizli) => {
            gizli = false;

            if (!mobilNavigasyonMu() || !mobilAramaAlani || mobilAramaAlani.classList.contains("ms-ana-navigasyon-arama-acik")) {
                mobilAramaAlani?.classList.remove("ms-ana-navigasyon-arama-gizli");
                mobilAramaUstAlani?.classList.remove("ms-ana-navigasyon-ust-arama-gizli");
                mobilAramaGizli = false;
                return;
            }

            if (mobilAramaGizli === gizli) {
                return;
            }

            mobilAramaGizli = gizli;
            mobilAramaAlani.classList.toggle("ms-ana-navigasyon-arama-gizli", gizli);
            mobilAramaUstAlani?.classList.toggle("ms-ana-navigasyon-ust-arama-gizli", gizli);
        };

        const mobilNavigasyonuGuncelle = () => {
            guncellemePlanlandi = false;

            if (!mobilNavigasyonMu() || document.body.classList.contains("ms-modal-acik")) {
                mobilMenuGorunumunuAyarla(false);
                mobilAramaGorunumunuAyarla(false);
                return;
            }

            const kullaniciKaydirdi = sonKaydirmaYonu !== 0
                && zamanDamgasiAl() - sonKaydirmaZamani < 600;

            if (!kullaniciKaydirdi) {
                return;
            }

            if (sonKaydirmaYonu > 0 && window.scrollY > 24) {
                mobilMenuGorunumunuAyarla(true);
                mobilAramaGorunumunuAyarla(true);
            } else if (sonKaydirmaYonu < 0 || window.scrollY < 8) {
                mobilMenuGorunumunuAyarla(false);
                mobilAramaGorunumunuAyarla(false);
            }
        };

        const mobilNavigasyonuPlanla = () => {
            if (guncellemePlanlandi) {
                return;
            }

            guncellemePlanlandi = true;
            requestAnimationFrame(mobilNavigasyonuGuncelle);
        };

        const kaydirmaYonunuIsaretle = (yon) => {
            if (yon === 0) {
                return;
            }

            sonKaydirmaYonu = yon;
            sonKaydirmaZamani = zamanDamgasiAl();
            mobilNavigasyonuPlanla();
        };

        window.addEventListener("scroll", mobilNavigasyonuPlanla, { passive: true });
        window.addEventListener("wheel", (event) => {
            if (Math.abs(event.deltaY) < 4) {
                return;
            }

            kaydirmaYonunuIsaretle(Math.sign(event.deltaY));
        }, { passive: true });
        window.addEventListener("touchstart", (event) => {
            dokunmaBaslangicY = event.touches[0]?.clientY || 0;
        }, { passive: true });
        window.addEventListener("touchmove", (event) => {
            const guncelDokunmaY = event.touches[0]?.clientY || 0;
            const dokunmaFarki = dokunmaBaslangicY - guncelDokunmaY;

            if (Math.abs(dokunmaFarki) < 8) {
                return;
            }

            kaydirmaYonunuIsaretle(Math.sign(dokunmaFarki));
            dokunmaBaslangicY = guncelDokunmaY;
        }, { passive: true });
        window.addEventListener("resize", () => {
            mobilMenuGorunumunuAyarla(false);
            mobilAramaGorunumunuAyarla(false);
        });
        window.addEventListener("pageshow", () => {
            mobilMenuGorunumunuAyarla(false);
            mobilAramaGorunumunuAyarla(false);
        });
    }

    panel.addEventListener("click", (event) => {
        if (event.target === panel) {
            panelKapat();
        }
    });

    document.addEventListener("pointerdown", (event) => {
        if (!kampanyaAlani?.classList.contains("ms-ana-navigasyon-mobil-kampanya-acik")) {
            return;
        }

        if (kampanyaAlani.contains(event.target) || kampanyaAcButonu?.contains(event.target)) {
            return;
        }

        kampanyaKapat();
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && panel.classList.contains("ms-ana-navigasyon-mobil-panel-acik")) {
            if (kampanyaAlani?.classList.contains("ms-ana-navigasyon-mobil-kampanya-acik")) {
                kampanyaKapat();
                return;
            }

            panelKapat();
        }
    });

    panelAc();
    };

    acButonu.addEventListener("click", mobilMenuBaslat, { once: true });
})();

// Ana navigasyon arama paneli, görsel arama sonuçları ve ürün kaydırma davranışları.
(() => {
    const aramaAlanlari = document.querySelectorAll("[data-ms-arama]");

    aramaAlanlari.forEach((aramaAlani) => {
        const input = aramaAlani.querySelector("[data-ms-arama-input]");
        const panel = aramaAlani.querySelector("[data-ms-arama-panel]");
        const kapat = aramaAlani.querySelector("[data-ms-arama-kapat]");
        const panelInput = aramaAlani.querySelector("[data-ms-arama-panel-input]");
        const populerAramalar = aramaAlani.querySelector("[data-ms-populer-aramalar]");
        const populerUrunler = aramaAlani.querySelector("[data-ms-populer-urunler]");
        const aramaSonuc = aramaAlani.querySelector("[data-ms-arama-sonuc]");
        const aramaSonucSayisi = aramaAlani.querySelector("[data-ms-arama-sonuc-sayisi]");
        const aramaSonucGruplari = aramaAlani.querySelector("[data-ms-arama-sonuc-gruplari]");
        const aramaSonucSablon = aramaAlani.querySelector("[data-ms-arama-sonuc-sablon]");
        const kategorideAraButonlari = aramaAlani.querySelectorAll("[data-ms-kategoride-ara]");
        const temizleButonlari = aramaAlani.querySelectorAll("[data-ms-arama-temizle]");
        const kameraButonlari = aramaAlani.querySelectorAll(".ms-ana-navigasyon-arama-kamera, .ms-ana-navigasyon-arama-panel-kamera");

        if (!input || !panel) {
            return;
        }

        const anaSayfaMi = window.location.pathname === "/" || window.location.pathname.toLowerCase() === "/home/index";
        const kategorideAraGosterilebilir = !anaSayfaMi;
        const varsayilanAramaSonucHtml = aramaSonucSablon instanceof HTMLTemplateElement
            ? aramaSonucSablon.innerHTML
            : aramaSonucGruplari?.innerHTML || "";
        const varsayilanAramaSonucSayisi = aramaSonucSayisi?.textContent || "15 ürün";
        let gorselAramaSonucuAktif = false;

        const varsayilanAramaSonuclariniHazirla = () => {
            if (aramaSonucGruplari && aramaSonucGruplari.childElementCount === 0) {
                aramaSonucGruplari.innerHTML = varsayilanAramaSonucHtml;
            }
        };

        [input, panelInput].filter(Boolean).forEach((aramaInput) => {
            const yazilabilirYap = () => {
                if (aramaInput === input && window.matchMedia("(max-width: 1023px)").matches) {
                    return;
                }

                aramaInput.removeAttribute("readonly");
            };

            aramaInput.addEventListener("pointerdown", yazilabilirYap, { capture: true });
            aramaInput.addEventListener("touchstart", yazilabilirYap, { capture: true, passive: true });
            aramaInput.addEventListener("focus", yazilabilirYap);
        });

        const varsayilanAramaSonuclariniYukle = () => {
            if (!gorselAramaSonucuAktif) {
                return;
            }

            if (aramaSonucGruplari) {
                aramaSonucGruplari.innerHTML = varsayilanAramaSonucHtml;
            }

            if (aramaSonucSayisi) {
                aramaSonucSayisi.textContent = varsayilanAramaSonucSayisi;
            }

            gorselAramaSonucuAktif = false;
        };

        const aramaDurumunuGuncelle = (ayarlar = {}) => {
            if (!ayarlar.gorselAramaSonucunuKoru) {
                varsayilanAramaSonuclariniYukle();
            }

            const aramaMetni = (panelInput?.value || input.value || "").trim();
            const sonucVar = aramaMetni.length > 0;

            if (sonucVar && !gorselAramaSonucuAktif) {
                varsayilanAramaSonuclariniHazirla();
            }

            aramaAlani.classList.toggle("ms-ana-navigasyon-arama-sonuclu", sonucVar);
            aramaAlani.classList.toggle("ms-ana-navigasyon-arama-yazili", sonucVar);

            temizleButonlari.forEach((temizleButonu) => {
                temizleButonu.hidden = !sonucVar;
            });

            if (populerAramalar) {
                populerAramalar.hidden = sonucVar;
            }

            if (aramaSonuc) {
                aramaSonuc.hidden = !sonucVar;
            }

            if (populerUrunler) {
                populerUrunler.hidden = sonucVar;
            }

            kategorideAraButonlari.forEach((kategorideAra) => {
                kategorideAra.hidden = !sonucVar || !kategorideAraGosterilebilir;

                // 2026-09-01 düzeltmesi: aktif sınıfı yalnız kategori bağlamı hiç olmayan
                // sayfalarda sökülür. Önceden "sonuç yokken" de sökülüyordu ama sonuç gelince
                // GERİ EKLENMİYORDU — kategori sayfasında varsayılan-aktif kapsam düğmesi
                // ("TESETTÜR içinde ara") turuncu yerine beyaz görünüyordu (kullanıcı bildirimi).
                // Kapsam durumunu (_AnaNavigasyonSearch) bölüm scripti yönetir; burada dokunulmaz.
                if (!kategorideAraGosterilebilir) {
                    kategorideAra.classList.remove("ms-ana-navigasyon-kategoride-ara-aktif");
                    kategorideAra.setAttribute("aria-pressed", "false");
                }
            });
        };

        const paneliAc = (ayarlar = {}) => {
            panel.classList.add("ms-ana-navigasyon-arama-panel-acik");
            aramaAlani.classList.add("ms-ana-navigasyon-arama-acik");
            if (panelInput) {
                panelInput.removeAttribute("readonly");
                panelInput.value = input.value;
            }

            aramaDurumunuGuncelle(ayarlar);

            if (panelInput && window.matchMedia("(max-width: 1023px)").matches) {
                window.setTimeout(() => panelInput.focus(), 40);
            }
        };

        input.addEventListener("pointerdown", (event) => {
            if (!window.matchMedia("(max-width: 1023px)").matches) {
                return;
            }

            event.preventDefault();
            paneliAc();
        });

        const paneliKapat = () => {
            panel.classList.remove("ms-ana-navigasyon-arama-panel-acik");
            aramaAlani.classList.remove("ms-ana-navigasyon-arama-acik");
        };

        const gorselAramaKartiOlustur = (sonuc, index) => {
            const kart = document.createElement("a");
            kart.className = "ms-search-urun-karti";
            kart.href = sonuc.productUrl || "/urun-detay";
            kart.setAttribute("aria-label", sonuc.productName || "Görsel arama ürün kartı");

            const gorselAlani = document.createElement("span");
            gorselAlani.className = "ms-search-urun-gorsel-alani";

            const gorsel = document.createElement("img");
            gorsel.className = "ms-search-urun-gorsel";
            gorsel.src = sonuc.imageUrl;
            gorsel.alt = sonuc.productName || `Görsel arama sonucu ${index + 1}`;

            const icerik = document.createElement("span");
            icerik.className = "ms-search-urun-icerik";

            const baslik = document.createElement("span");
            baslik.className = "ms-search-urun-baslik";

            const marka = document.createElement("strong");
            marka.textContent = "Misharix";
            baslik.append(marka, ` ${sonuc.productName || "Ürün"}`);

            const meta = document.createElement("span");
            meta.className = "ms-search-urun-meta";
            meta.textContent = sonuc.modelCode || "Model kodu yok";

            const fiyat = document.createElement("span");
            fiyat.className = "ms-search-urun-fiyat ms-urun-fiyat";
            fiyat.textContent = typeof sonuc.price === "number"
                ? new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(sonuc.price) + " TL"
                : "Fiyat bilgisi yok";

            icerik.append(baslik, meta, fiyat);

            gorselAlani.appendChild(gorsel);
            kart.append(gorselAlani, icerik);

            return kart;
        };

        const gorselAramaSonuclariniGoster = (sonuclar) => {
            if (!aramaSonuc || !aramaSonucGruplari) {
                return;
            }

            const resimliSonuclar = (sonuclar || []).filter((sonuc) => Boolean(sonuc.imageUrl));
            const sonucMetni = `${resimliSonuclar.length} ürün`;

            input.value = "Görsel arama";

            if (panelInput) {
                panelInput.value = "Görsel arama";
            }

            if (aramaSonucSayisi) {
                aramaSonucSayisi.textContent = sonucMetni;
            }

            aramaSonucGruplari.innerHTML = "";

            const grup = document.createElement("section");
            grup.className = "ms-ana-navigasyon-arama-sonuc-grubu";

            const label = document.createElement("div");
            label.className = "ms-ana-navigasyon-arama-kategori-label";
            label.innerHTML = `<span>Görsel Arama</span><small>${sonucMetni}</small>`;

            const liste = document.createElement("div");
            liste.className = "ms-ana-navigasyon-arama-sonuc-listesi";

            if (resimliSonuclar.length === 0) {
                const bos = document.createElement("p");
                bos.className = "ms-gorsel-arama-bos-sonuc";
                bos.textContent = "Görsel arama için eşleşen ürün bulunamadı.";
                liste.appendChild(bos);
            } else {
                resimliSonuclar.forEach((sonuc, index) => {
                    liste.appendChild(gorselAramaKartiOlustur(sonuc, index));
                });
            }

            grup.append(label, liste);
            aramaSonucGruplari.appendChild(grup);
            gorselAramaSonucuAktif = true;

            aramaAlani.classList.add("ms-ana-navigasyon-arama-sonuclu");
            aramaSonuc.hidden = false;

            if (populerAramalar) {
                populerAramalar.hidden = true;
            }

            if (populerUrunler) {
                populerUrunler.hidden = true;
            }

            kategorideAraButonlari.forEach((kategorideAra) => {
                kategorideAra.hidden = !kategorideAraGosterilebilir;
            });

            paneliAc({ gorselAramaSonucunuKoru: true });
        };

        input.addEventListener("focus", paneliAc);
        input.addEventListener("click", paneliAc);
        input.addEventListener("input", () => {
            if (panelInput) {
                panelInput.value = input.value;
            }

            varsayilanAramaSonuclariniYukle();
            aramaDurumunuGuncelle();
        });
        panelInput?.addEventListener("input", () => {
            input.value = panelInput.value;
            varsayilanAramaSonuclariniYukle();
            aramaDurumunuGuncelle();
        });

        temizleButonlari.forEach((temizleButonu) => {
            temizleButonu.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                input.value = "";

                if (panelInput) {
                    panelInput.value = "";
                }

                varsayilanAramaSonuclariniYukle();
                aramaDurumunuGuncelle();
                (panel.classList.contains("ms-ana-navigasyon-arama-panel-acik") ? panelInput : input)?.focus();
            });
        });

        kapat?.addEventListener("click", () => {
            paneliKapat();
            input.blur();
        });

        kameraButonlari.forEach((buton) => {
            buton.addEventListener("click", (event) => {
                event.preventDefault();
                event.stopPropagation();
                paneliKapat();
                document.dispatchEvent(new CustomEvent("ms:gorsel-arama-ac"));
            });
        });

        document.addEventListener("ms:gorsel-arama-sonuc", (event) => {
            gorselAramaSonuclariniGoster(event.detail?.results || []);
        });

        kategorideAraButonlari.forEach((kategorideAra) => {
            kategorideAra.addEventListener("click", () => {
                const aktif = !kategorideAra.classList.contains("ms-ana-navigasyon-kategoride-ara-aktif");

                kategorideAraButonlari.forEach((buton) => {
                    buton.classList.toggle("ms-ana-navigasyon-kategoride-ara-aktif", aktif);
                    buton.setAttribute("aria-pressed", aktif ? "true" : "false");
                });
            });
        });

        document.addEventListener("pointerdown", (event) => {
            if (!aramaAlani.contains(event.target)) {
                paneliKapat();
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                paneliKapat();
                input.blur();
            }
        });

        aramaAlani.querySelectorAll("[data-ms-arama-urun-listesi]").forEach((liste) => {
            const kaydirmaAlani = liste.closest(".ms-ana-navigasyon-urun-kaydirma-alani");
            const kontroller = kaydirmaAlani?.querySelectorAll("[data-ms-arama-urun-kaydir]") || [];
            let surukleniyor = false;
            let baslangicX = 0;
            let baslangicScroll = 0;
            let tiklamaEngellenecek = false;

            const kaydirmaDurumuGuncelle = () => {
                if (kontroller.length === 0) {
                    return;
                }

                const kaydirilabilir = liste.scrollWidth > liste.clientWidth + 1;
                const enSolda = liste.scrollLeft <= 1;
                const enSagda = liste.scrollLeft + liste.clientWidth >= liste.scrollWidth - 1;

                kontroller.forEach((kontrol) => {
                    kontrol.classList.toggle("ms-ana-navigasyon-urun-kontrol-aktif", kaydirilabilir);
                    kontrol.disabled = !kaydirilabilir
                        || (kontrol.dataset.msAramaUrunKaydir === "sol" && enSolda)
                        || (kontrol.dataset.msAramaUrunKaydir === "sag" && enSagda);
                });
            };

            kontroller.forEach((kontrol) => {
                kontrol.addEventListener("click", () => {
                    const yon = kontrol.dataset.msAramaUrunKaydir === "sol" ? -1 : 1;
                    liste.scrollTo({
                        left: liste.scrollLeft + (liste.clientWidth * 0.8 * yon),
                        behavior: "smooth"
                    });
                });
            });

            liste.addEventListener("dragstart", (event) => event.preventDefault());

            liste.addEventListener("pointerdown", (event) => {
                if (event.button !== 0) {
                    return;
                }

                surukleniyor = true;
                baslangicX = event.clientX;
                baslangicScroll = liste.scrollLeft;
                tiklamaEngellenecek = false;
                liste.classList.add("ms-ana-navigasyon-urun-listesi-surukleniyor");
                liste.setPointerCapture(event.pointerId);
            });

            liste.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                const fark = event.clientX - baslangicX;

                if (Math.abs(fark) > 5) {
                    tiklamaEngellenecek = true;
                }

                liste.scrollLeft = baslangicScroll - fark;
            });

            const suruklemeyiBitir = (event) => {
                if (!surukleniyor) {
                    return;
                }

                surukleniyor = false;
                liste.classList.remove("ms-ana-navigasyon-urun-listesi-surukleniyor");

                if (liste.hasPointerCapture(event.pointerId)) {
                    liste.releasePointerCapture(event.pointerId);
                }
            };

            liste.addEventListener("pointerup", suruklemeyiBitir);
            liste.addEventListener("pointercancel", suruklemeyiBitir);
            liste.addEventListener("scroll", kaydirmaDurumuGuncelle, { passive: true });
            window.addEventListener("resize", kaydirmaDurumuGuncelle);
            liste.addEventListener("click", (event) => {
                if (!tiklamaEngellenecek) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();
                tiklamaEngellenecek = false;
            }, true);
            requestAnimationFrame(kaydirmaDurumuGuncelle);
        });
    });
})();

// Ana navigasyon sepet menüsü aç/kapat davranışı.
(() => {
    const sepetMenuleri = document.querySelectorAll("[data-ms-sepet-menu]");

    sepetMenuleri.forEach((menu) => {
        const tetikleyici = menu.querySelector("[data-ms-sepet-menu-tetikleyici]");
        const panel = menu.querySelector("[data-ms-sepet-menu-panel]");
        const kapatButonu = menu.querySelector("[data-ms-sepet-panel-kapat]");
        const urunListesi = menu.querySelector("[data-ms-sepet-urun-listesi]");
        const urunSablonu = menu.querySelector("[data-ms-sepet-urun-sablon]");
        const urunSayisi = menu.querySelector("[data-ms-sepet-urun-sayisi]");
        const sepetRozeti = menu.querySelector("[data-ms-sepet-rozet]");
        const sepetToplami = menu.querySelector("[data-ms-sepet-toplam]");

        if (!tetikleyici) {
            return;
        }

        const menuKapat = () => {
            menu.classList.remove("ms-ana-navigasyon-sepet-acik");
            tetikleyici.setAttribute("aria-expanded", "false");
        };

        const fiyatSayiyaCevir = (metin) => {
            const temizMetin = String(metin || "")
                .replace(/[^\d,.-]/g, "")
                .replace(/\./g, "")
                .replace(",", ".");

            return Number.parseFloat(temizMetin) || 0;
        };

        const sepetOzetiniGuncelle = () => {
            if (!urunListesi) {
                return;
            }

            const urunler = Array.from(urunListesi.querySelectorAll(".ms-ana-navigasyon-sepet-urun"));
            const adet = urunler.length;
            const toplam = urunler.reduce((deger, urun) => {
                const fiyat = urun.querySelector(".ms-ana-navigasyon-sepet-urun-alt strong");
                return deger + fiyatSayiyaCevir(fiyat?.textContent);
            }, 0);

            if (urunSayisi) {
                urunSayisi.textContent = `${adet} ürün`;
            }

            if (sepetRozeti) {
                sepetRozeti.textContent = adet.toString();
                sepetRozeti.hidden = adet === 0;
            }

            if (sepetToplami) {
                sepetToplami.textContent = `${toplam.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`;
            }

            let bosDurum = urunListesi.querySelector("[data-ms-sepet-bos]");

            if (adet === 0 && !bosDurum) {
                bosDurum = document.createElement("p");
                bosDurum.className = "ms-ana-navigasyon-sepet-bos";
                bosDurum.dataset.msSepetBos = "";
                bosDurum.textContent = "Sepetinizde ürün bulunmuyor.";
                urunListesi.appendChild(bosDurum);
            } else if (adet > 0) {
                bosDurum?.remove();
            }
        };

        const menuToggle = () => {
            if (!menu.classList.contains("ms-ana-navigasyon-sepet-acik")
                && urunListesi
                && urunListesi.dataset.msSepetYuklendi !== "true"
                && urunSablonu instanceof HTMLTemplateElement) {
                urunListesi.appendChild(urunSablonu.content.cloneNode(true));
                urunListesi.dataset.msSepetYuklendi = "true";
                sepetOzetiniGuncelle();
            }

            const acik = menu.classList.toggle("ms-ana-navigasyon-sepet-acik");
            tetikleyici.setAttribute("aria-expanded", acik ? "true" : "false");
        };

        tetikleyici.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
        });

        tetikleyici.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            menuToggle();
        });

        panel?.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
        });

        panel?.addEventListener("click", (event) => {
            event.stopPropagation();
        });

        urunListesi?.addEventListener("click", (event) => {
            const silButonu = event.target instanceof Element
                ? event.target.closest(".ms-ana-navigasyon-sepet-sil")
                : null;

            if (!silButonu || !urunListesi.contains(silButonu)) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            silButonu.closest(".ms-ana-navigasyon-sepet-urun")?.remove();
            sepetOzetiniGuncelle();
        });

        kapatButonu?.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            menuKapat();
            tetikleyici.blur();
        });

        document.addEventListener("pointerdown", (event) => {
            if (!menu.contains(event.target)) {
                menuKapat();
            }
        });

        window.addEventListener("scroll", () => {
            if (menu.classList.contains("ms-ana-navigasyon-sepet-acik")) {
                menuKapat();
            }
        }, { passive: true });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                menuKapat();
                tetikleyici.blur();
            }
        });
    });
})();

// Mağaza üst menü, mega menü ve kampanya kaydırma davranışları.
(() => {
    const menuler = document.querySelectorAll("[data-ms-magaza-menu]");

    menuler.forEach((menu) => {
        const anaMenuLink = menu.querySelector(".ms-magaza-menu-tum > .ms-magaza-menu-link");
        let magazaMenuBaslatma = null;
        let menuAcKapat = null;

        const magazaMenuBaslat = () => {
        if (magazaMenuBaslatma) {
            return magazaMenuBaslatma;
        }

        magazaMenuBaslatma = (async () => {
        const sablon = menu.querySelector("[data-ms-magaza-mega-menu-sablon]");

        if (sablon instanceof HTMLTemplateElement) {
            sablon.parentNode?.insertBefore(sablon.content.cloneNode(true), sablon);
            sablon.remove();
        } else if (!menu.querySelector("[data-ms-magaza-mega-menu]")) {
            const hedef = menu.querySelector("[data-ms-magaza-mega-menu-hedef]");
            const url = menu.dataset.msMegaMenuUrl;

            if (!hedef || !url) {
                return false;
            }

            hedef.setAttribute("aria-busy", "true");
            const yanit = await fetch(url, {
                headers: { "Accept": "text/html" },
                credentials: "same-origin"
            });
            if (!yanit.ok) {
                throw new Error(`Mega menü yüklenemedi (${yanit.status}).`);
            }

            hedef.innerHTML = await yanit.text();
            hedef.removeAttribute("aria-busy");
        }

        const megaMenu = menu.querySelector("[data-ms-magaza-mega-menu]");
        const ustLinkler = menu.querySelectorAll("[data-ms-magaza-menu-link]");
        const kampanyaListesi = menu.querySelector(".ms-magaza-mega-kampanya-listesi");
        const kampanyaKontrolleri = menu.querySelectorAll("[data-ms-kampanya-kaydir]");
        const menuIc = menu.querySelector(".ms-magaza-menu-ic");

        if (!megaMenu || !anaMenuLink) {
            return false;
        }

        const solKolon = document.createElement("div");
        solKolon.className = "ms-magaza-mega-sol-kolon";

        Array.from(megaMenu.querySelectorAll(":scope > .ms-magaza-mega-kategori-grubu")).forEach((grup) => {
            const kategori = grup.dataset.msMagazaKategoriGrubu;
            const panel = grup.querySelector(".ms-magaza-mega-icerik");

            if (panel && kategori) {
                panel.dataset.msMagazaPanel = kategori;
                megaMenu.appendChild(panel);
            }

            solKolon.appendChild(grup);
        });

        megaMenu.prepend(solKolon);

        const solLinkler = megaMenu.querySelectorAll("[data-ms-magaza-kategori]");
        let menuKaydirma = menu.querySelector("[data-ms-magaza-menu-kaydirma]");
        let menuKaydirmaGrubu = menuKaydirma?.closest(".ms-magaza-menu-kaydirma-grubu") || null;
        let menuKaydirmaKontrolleri = menu.querySelector("[data-ms-magaza-menu-kaydirma-kontrolleri]");
        let menuKaydirmaTiklamayiEngelle = false;

        if (menuIc && ustLinkler.length > 0) {
            if (!menuKaydirma || !menuKaydirmaGrubu) {
            menuKaydirmaGrubu = document.createElement("div");
            menuKaydirmaGrubu.className = "ms-magaza-menu-kaydirma-grubu";

            menuKaydirma = document.createElement("div");
            menuKaydirma.className = "ms-magaza-menu-kaydirma";
            menuKaydirma.dataset.msMagazaMenuKaydirma = "";

            menuKaydirmaGrubu.appendChild(menu.querySelector(".ms-magaza-menu-tum"));
            ustLinkler.forEach((link) => menuKaydirma.appendChild(link));
            menuKaydirmaGrubu.appendChild(menuKaydirma);
            menuIc.appendChild(menuKaydirmaGrubu);
            }

            if (!menuKaydirmaKontrolleri) {
            menuKaydirmaKontrolleri = document.createElement("div");
            menuKaydirmaKontrolleri.className = "ms-magaza-menu-kaydirma-kontrolleri";
            menuKaydirmaKontrolleri.dataset.msMagazaMenuKaydirmaKontrolleri = "";
            menuKaydirmaKontrolleri.innerHTML = `
                <button class="ms-magaza-menu-kaydirma-kontrol" type="button" aria-label="Menüyü sola kaydır" data-ms-menu-kaydir="sol">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" aria-hidden="true">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
                    </svg>
                </button>
                <button class="ms-magaza-menu-kaydirma-kontrol" type="button" aria-label="Menüyü sağa kaydır" data-ms-menu-kaydir="sag">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.8" stroke="currentColor" aria-hidden="true">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                    </svg>
                </button>
            `;
            menuIc.appendChild(menuKaydirmaKontrolleri);
            }
        }

        const menuKaydirmaKontrolDurumuGuncelle = (kaydirilabilir) => {
            if (!menuKaydirma || !menuKaydirmaKontrolleri) {
                return;
            }

            const solKontrol = menuKaydirmaKontrolleri.querySelector("[data-ms-menu-kaydir='sol']");
            const sagKontrol = menuKaydirmaKontrolleri.querySelector("[data-ms-menu-kaydir='sag']");
            const enSolda = menuKaydirma.scrollLeft <= 1;
            const enSagda = menuKaydirma.scrollLeft + menuKaydirma.clientWidth >= menuKaydirma.scrollWidth - 1;

            if (solKontrol) {
                solKontrol.disabled = !kaydirilabilir || enSolda;
            }

            if (sagKontrol) {
                sagKontrol.disabled = !kaydirilabilir || enSagda;
            }
        };

        const menuKaydirmaDurumuGuncelle = () => {
            if (!menuKaydirma || !menuKaydirmaKontrolleri) {
                return;
            }

            const tumMenuGenisligi = menu.querySelector(".ms-magaza-menu-tum")?.getBoundingClientRect().width || 0;
            const kontrollerGenisligi = 80;
            const boslukPayi = 40;
            const kullanilabilirGenislik = Math.max(menuIc.clientWidth - tumMenuGenisligi - kontrollerGenisligi - boslukPayi, 120);
            const kaydirilabilir = menuKaydirma.scrollWidth > kullanilabilirGenislik + 1;

            menuIc?.classList.toggle("ms-magaza-menu-ic-kaydirilabilir", kaydirilabilir);
            menuKaydirmaGrubu?.classList.toggle("ms-magaza-menu-kaydirma-grubu-kaydirilabilir", kaydirilabilir);
            menuKaydirma.classList.toggle("ms-magaza-menu-kaydirma-kaydirilabilir", kaydirilabilir);
            menuKaydirmaKontrolleri.classList.toggle("ms-magaza-menu-kaydirma-kontrolleri-aktif", kaydirilabilir);

            if (!kaydirilabilir) {
                menuKaydirma.scrollLeft = 0;
            }

            requestAnimationFrame(() => menuKaydirmaKontrolDurumuGuncelle(kaydirilabilir));
        };

        const kampanyaKaydirmaDurumuGuncelle = () => {
            if (!kampanyaListesi || kampanyaKontrolleri.length === 0) {
                return;
            }

            const kaydirilabilir = kampanyaListesi.scrollWidth > kampanyaListesi.clientWidth + 1;
            kampanyaListesi.classList.toggle("ms-magaza-mega-kampanya-listesi-kaydirilabilir", kaydirilabilir);
            kampanyaKontrolleri.forEach((kontrol) => {
                kontrol.classList.toggle("ms-magaza-mega-kampanya-kontrol-aktif", kaydirilabilir);
                kontrol.disabled = !kaydirilabilir;
            });
        };

        const menuAc = () => {
            megaMenu.classList.add("ms-magaza-mega-menu-acik");
            requestAnimationFrame(kampanyaKaydirmaDurumuGuncelle);
            requestAnimationFrame(menuKaydirmaDurumuGuncelle);
        };

        const kategoriAc = (kategori, solLinkeKaydir = false) => {
            const hedefGrup = menu.querySelector(`[data-ms-magaza-kategori-grubu="${kategori}"]`);
            const hedefPanel = megaMenu.querySelector(`[data-ms-magaza-panel="${kategori}"]`);
            const hedefSolLink = hedefGrup?.querySelector(".ms-magaza-mega-sol-link");

            if (!hedefPanel) {
                return;
            }

            menu.querySelectorAll(".ms-magaza-mega-icerik").forEach((panel) => {
                panel.classList.remove("ms-magaza-mega-icerik-aktif");
            });

            menu.querySelectorAll(".ms-magaza-mega-sol-link").forEach((link) => {
                link.classList.remove("ms-magaza-mega-sol-link-aktif");
            });

            ustLinkler.forEach((link) => {
                link.classList.toggle("ms-magaza-menu-link-aktif", link.dataset.msMagazaMenuLink === kategori);
            });

            hedefPanel.classList.add("ms-magaza-mega-icerik-aktif");
            hedefSolLink?.classList.add("ms-magaza-mega-sol-link-aktif");

            if (solLinkeKaydir && hedefGrup && solKolon) {
                requestAnimationFrame(() => {
                    solKolon.scrollTo({
                        top: Math.max(hedefGrup.offsetTop - 4, 0),
                        behavior: "smooth"
                    });
                });
            }
        };

        const kategoriKapat = () => {
            megaMenu.classList.remove("ms-magaza-mega-menu-acik");
            menu.querySelectorAll(".ms-magaza-mega-icerik").forEach((panel) => {
                panel.classList.remove("ms-magaza-mega-icerik-aktif");
            });
            menu.querySelectorAll(".ms-magaza-mega-sol-link").forEach((link) => {
                link.classList.remove("ms-magaza-mega-sol-link-aktif");
            });
            ustLinkler.forEach((link) => link.classList.remove("ms-magaza-menu-link-aktif"));
        };

        menuAcKapat = () => {
            if (megaMenu.classList.contains("ms-magaza-mega-menu-acik")) {
                kategoriKapat();
            } else {
                menuAc();
            }
        };

        // Mega menü hover ayarı (2026-08-14, data-ms-mega-hover panel Menü Yerleşimi'nden):
        // kapalıyken (varsayılan) üzerine gelmek mega menüyü AÇMAZ; mega menü yalnız
        // "Kategoriler" tıklamasıyla açılır/kapanır. Menü linkleri her iki modda da
        // tıklamada gerçek navigasyon yapar (ürün listesi sayfası açılır).
        const hoverIleAcilir = menu.dataset.msMegaHover === "1";

        if (hoverIleAcilir) {
            anaMenuLink.addEventListener("mouseenter", menuAc);
            anaMenuLink.addEventListener("focus", menuAc);
        }

        ustLinkler.forEach((link) => {
            const kategori = link.dataset.msMagazaMenuLink;

            if (hoverIleAcilir) {
                link.addEventListener("mouseenter", () => {
                    menuAc();
                    kategoriAc(kategori, true);
                });
                link.addEventListener("focus", () => {
                    menuAc();
                    kategoriAc(kategori, true);
                });
            }
            link.addEventListener("click", (event) => {
                if (menuKaydirmaTiklamayiEngelle) {
                    event.preventDefault();
                    event.stopPropagation();
                    menuKaydirmaTiklamayiEngelle = false;
                    return;
                }
                // tıklama = navigasyon (href kategorinin ürün listesi) — mega menü açmaz
            });
        });

        solLinkler.forEach((link) => {
            const kategori = link.dataset.msMagazaKategori;

            link.addEventListener("mouseenter", () => {
                menuAc();
                kategoriAc(kategori);
            });
            // tıklama = navigasyon (href kategorinin ürün listesi) — preventDefault kaldırıldı
        });

        // Endpoint cevabı gelene kadar ilk mouseenter/focus olayı tamamlanmış olabilir.
        // Hover modu açıksa o anda üzerinde/odakta olunan linkin beklenen panelini aç.
        if (hoverIleAcilir) {
            const etkinUstLink = Array.from(ustLinkler).find((link) =>
                link.matches(":hover") || link === document.activeElement);
            if (etkinUstLink) {
                menuAc();
                kategoriAc(etkinUstLink.dataset.msMagazaMenuLink, true);
            } else if (anaMenuLink.matches(":hover") || anaMenuLink === document.activeElement) {
                menuAc();
            }
        }

        menu.addEventListener("mouseleave", kategoriKapat);

        if (menuKaydirma && menuKaydirmaKontrolleri) {
            menuKaydirmaKontrolleri.querySelectorAll("[data-ms-menu-kaydir]").forEach((kontrol) => {
                kontrol.addEventListener("click", (event) => {
                    event.preventDefault();
                    const yon = kontrol.dataset.msMenuKaydir === "sol" ? -1 : 1;
                    const maksimumScroll = menuKaydirma.scrollWidth - menuKaydirma.clientWidth;
                    if (maksimumScroll <= 0) {
                        menuKaydirmaDurumuGuncelle();
                        return;
                    }

                    const hedefScroll = Math.min(Math.max(menuKaydirma.scrollLeft + (menuKaydirma.clientWidth * 0.7 * yon), 0), maksimumScroll);

                    menuKaydirma.scrollTo({
                        left: hedefScroll,
                        behavior: "smooth"
                    });

                    window.setTimeout(() => menuKaydirmaKontrolDurumuGuncelle(true), 220);
                });
            });

            let menuSurukleniyor = false;
            let menuBaslangicX = 0;
            let menuBaslangicScroll = 0;

            menuKaydirma.addEventListener("dragstart", (event) => event.preventDefault());

            menuKaydirma.addEventListener("pointerdown", (event) => {
                if (event.button !== 0) {
                    return;
                }

                // 2026-08-14: preventDefault + setPointerCapture BURADA YAPILMAZ — pointerdown
                // anında yakalamak tarayıcının click olayını linke değil bu konteynere
                // hedeflemesine yol açıyor ve menü linkinin navigasyonu hiç çalışmıyordu.
                // Sürükleme ancak eşik (5px) aşılınca yakalanır; basit tıklama linke ulaşır.
                menuSurukleniyor = true;
                menuBaslangicX = event.clientX;
                menuBaslangicScroll = menuKaydirma.scrollLeft;
                menuKaydirmaTiklamayiEngelle = false;
            });

            menuKaydirma.addEventListener("pointermove", (event) => {
                if (!menuSurukleniyor) {
                    return;
                }

                const fark = event.clientX - menuBaslangicX;

                if (Math.abs(fark) > 5 && !menuKaydirmaTiklamayiEngelle) {
                    menuKaydirmaTiklamayiEngelle = true;
                    menuKaydirma.classList.add("ms-magaza-menu-kaydirma-surukleniyor");
                    menuKaydirma.setPointerCapture?.(event.pointerId);
                }

                if (!menuKaydirmaTiklamayiEngelle) {
                    return;
                }

                menuKaydirma.scrollLeft = menuBaslangicScroll - fark;
                menuKaydirmaKontrolDurumuGuncelle(true);
            });

            const menuSuruklemeyiBitir = (event) => {
                if (!menuSurukleniyor) {
                    return;
                }

                menuSurukleniyor = false;
                menuKaydirma.classList.remove("ms-magaza-menu-kaydirma-surukleniyor");

                if (menuKaydirma.hasPointerCapture?.(event.pointerId)) {
                    menuKaydirma.releasePointerCapture(event.pointerId);
                }
            };

            menuKaydirma.addEventListener("pointerup", menuSuruklemeyiBitir);
            menuKaydirma.addEventListener("pointercancel", menuSuruklemeyiBitir);
            menuKaydirma.addEventListener("scroll", menuKaydirmaDurumuGuncelle, { passive: true });
            window.addEventListener("resize", menuKaydirmaDurumuGuncelle);
            requestAnimationFrame(menuKaydirmaDurumuGuncelle);
        }

        kampanyaKontrolleri.forEach((kontrol) => kontrol.addEventListener("click", () => {
            if (!kampanyaListesi) {
                return;
            }

            const yon = kontrol.dataset.msKampanyaKaydir === "sol" ? -1 : 1;

            kampanyaListesi.scrollTo({
                left: kampanyaListesi.scrollLeft + (kampanyaListesi.clientWidth * 0.75 * yon),
                behavior: "smooth"
            });
        }));

        if (kampanyaListesi) {
            let surukleniyor = false;
            let baslangicX = 0;
            let baslangicScroll = 0;
            let suruklemeTiklamayiEngelle = false;

            kampanyaListesi.addEventListener("dragstart", (event) => {
                event.preventDefault();
            });

            kampanyaListesi.addEventListener("pointerdown", (event) => {
                if (event.button !== 0) {
                    return;
                }

                // 2026-08-14: yakalama eşik aşılınca — pointerdown'da capture, click'i
                // konteynere retarget edip kampanya linklerinin navigasyonunu öldürüyordu
                // (menü kaydırma şeridiyle aynı düzeltme).
                surukleniyor = true;
                baslangicX = event.clientX;
                baslangicScroll = kampanyaListesi.scrollLeft;
                suruklemeTiklamayiEngelle = false;
            });

            kampanyaListesi.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                const fark = event.clientX - baslangicX;
                if (Math.abs(fark) > 5 && !suruklemeTiklamayiEngelle) {
                    suruklemeTiklamayiEngelle = true;
                    kampanyaListesi.classList.add("ms-magaza-mega-kampanya-listesi-surukleniyor");
                    kampanyaListesi.setPointerCapture(event.pointerId);
                }

                if (!suruklemeTiklamayiEngelle) {
                    return;
                }

                kampanyaListesi.scrollLeft = baslangicScroll - fark;
            });

            const kampanyaSuruklemeBitir = (event) => {
                if (!surukleniyor) {
                    return;
                }

                surukleniyor = false;
                kampanyaListesi.classList.remove("ms-magaza-mega-kampanya-listesi-surukleniyor");

                if (kampanyaListesi.hasPointerCapture(event.pointerId)) {
                    kampanyaListesi.releasePointerCapture(event.pointerId);
                }
            };

            kampanyaListesi.addEventListener("pointerup", kampanyaSuruklemeBitir);
            kampanyaListesi.addEventListener("pointercancel", kampanyaSuruklemeBitir);
            kampanyaListesi.addEventListener("click", (event) => {
                if (!suruklemeTiklamayiEngelle) {
                    return;
                }

                event.preventDefault();
                event.stopPropagation();
                suruklemeTiklamayiEngelle = false;
            }, true);
        }

        window.addEventListener("resize", kampanyaKaydirmaDurumuGuncelle);
        kampanyaKaydirmaDurumuGuncelle();

        document.addEventListener("pointerdown", (event) => {
            if (!menu.contains(event.target)) {
                kategoriKapat();
            }
        });
        return true;
        })().catch(() => {
            menu.querySelector("[data-ms-magaza-mega-menu-hedef]")?.removeAttribute("aria-busy");
            magazaMenuBaslatma = null;
            return false;
        });

        return magazaMenuBaslatma;
        };

        anaMenuLink?.addEventListener("click", async (event) => {
            event.preventDefault();
            if (await magazaMenuBaslat()) {
                menuAcKapat?.();
            } else {
                window.location.assign(anaMenuLink.href);
            }
        });

        // Hover ile mega menü kapalıyken normal üst menü linkleri yalnız navigasyon yapar.
        // Mega menü HTML'ini yalnız Kategoriler gerçekten kullanılacağı zaman endpoint'ten
        // getir. Hover modu açıksa normal üst menü etkileşimi de ön yüklemeyi başlatır.
        const megaMenuTetikAlani = menu.dataset.msMegaHover === "1"
            ? menu
            : menu.querySelector(".ms-magaza-menu-tum");

        if (menu.dataset.msMegaHover === "1") {
            megaMenuTetikAlani?.addEventListener("pointerenter", magazaMenuBaslat, { once: true });
        }
        megaMenuTetikAlani?.addEventListener("focusin", magazaMenuBaslat, { once: true });
        megaMenuTetikAlani?.addEventListener("pointerdown", magazaMenuBaslat, { once: true, capture: true });
    });
})();

// Giriş, kayıt, belge modalı ve hesap oturumu örnek davranışları.
(() => {
    let girisDavranislariBaslatildi = false;

    const girisDavranislariniBaslat = () => {
    if (girisDavranislariBaslatildi) {
        return;
    }

    girisDavranislariBaslatildi = true;
    document.querySelectorAll("[data-ms-giris-modaller-sablon]").forEach((sablon) => {
        if (sablon instanceof HTMLTemplateElement) {
            sablon.parentNode?.insertBefore(sablon.content.cloneNode(true), sablon);
            sablon.remove();
        }
    });

    const girisMenuleri = document.querySelectorAll("[data-ms-giris-menu]");
    const modal = document.querySelector("[data-ms-giris-modal]");
    const modalAcButonlari = document.querySelectorAll("[data-ms-giris-modal-ac]");
    const kayitModal = document.querySelector("[data-ms-kayit-modal]");
    const kayitModalAcButonlari = document.querySelectorAll("[data-ms-kayit-modal-ac]");
    const kayitModalKapaticilar = kayitModal ? kayitModal.querySelectorAll("[data-ms-kayit-modal-kapat]") : [];
    const belgeModal = document.querySelector("[data-ms-belge-modal]");
    const belgeModalAcButonlari = document.querySelectorAll("[data-ms-belge-modal-ac]");
    const belgeModalKapaticilar = belgeModal ? belgeModal.querySelectorAll("[data-ms-belge-modal-kapat]") : [];
    const belgeModalBaslik = belgeModal ? belgeModal.querySelector("[data-ms-belge-modal-baslik]") : null;
    const belgeModalIcerik = belgeModal ? belgeModal.querySelector("[data-ms-belge-modal-icerik]") : null;
    const belgeModalKabul = belgeModal ? belgeModal.querySelector("[data-ms-belge-modal-kabul]") : null;
    const modalKapaticilar = modal ? modal.querySelectorAll("[data-ms-giris-modal-kapat]") : [];
    const tablar = modal ? modal.querySelectorAll("[data-ms-giris-tab]") : [];
    const paneller = modal ? modal.querySelectorAll("[data-ms-giris-panel]") : [];
    const smsTelefonAdim = modal ? modal.querySelector('[data-ms-giris-sms-adim="telefon"]') : null;
    const smsKodAdim = modal ? modal.querySelector('[data-ms-giris-sms-adim="kod"]') : null;
    const kodGonder = modal ? modal.querySelector("[data-ms-giris-kod-gonder]") : null;
    const kodOnayla = modal ? modal.querySelector("[data-ms-giris-kod-onayla]") : null;
    const smsGeri = modal ? modal.querySelector("[data-ms-giris-sms-geri]") : null;
    const smsTelefon = modal ? modal.querySelector("[data-ms-giris-telefon]") : null;
    const kodInputlari = modal ? Array.from(modal.querySelectorAll(".ms-giris-kod-input")) : [];
    const kodSayac = modal ? modal.querySelector("[data-ms-giris-kod-sayac]") : null;
    const kodSayacAlani = kodSayac?.closest(".ms-giris-kod-sayac") || null;
    const kodYenidenGonder = modal ? modal.querySelector("[data-ms-giris-kod-yeniden-gonder]") : null;
    const smsKodOdakDisiAlanlar = modal ? [
        modal.querySelector(".ms-giris-tab-listesi"),
        modal.querySelector(".ms-giris-ayrac"),
        modal.querySelector(".ms-giris-sosyal-listesi"),
        modal.querySelector(".ms-giris-buton-misafir"),
        modal.querySelector(".ms-giris-buton-kayit")
    ].filter(Boolean) : [];
    let kodSayacTimer = null;
    let kodKalanSaniye = 120;
    let sonOdaklananEleman = null;
    let aktifBelgeCheckbox = null;

    const belgeModalAcikMi = () => belgeModal?.classList.contains("ms-ornek-modal-acik");

    const belgeIcerikleri = {
        uyelik: `
            <p>Üyelik Sözleşmesi; hesabınızın oluşturulması, sipariş süreçleri, üyelik hakları ve kullanım koşulları hakkında bilgilendirme içerir.</p>
            <p>Kabul ettiğinizde bu metni okuduğunuz ve üyelik işlemi için gerekli koşulları onayladığınız kabul edilir.</p>
        `,
        aydinlatma: `
            <p>Aydınlatma Metni; kişisel verilerinizin hangi amaçlarla işlendiği, saklandığı ve hangi haklara sahip olduğunuz hakkında bilgi verir.</p>
            <p>Kabul ettiğinizde kişisel verilerin işlenmesine ilişkin bilgilendirmeyi okuduğunuz işaretlenir.</p>
        `,
        "on-bilgilendirme": `
            <p>Ön Bilgilendirme Formu; sipariş, ödeme, teslimat, iade ve cayma hakkı süreçlerine dair temel bilgileri içerir.</p>
            <p>Kabul ettiğinizde alışveriş öncesi bilgilendirme metnini okuduğunuz kabul edilir.</p>
        `
    };

    girisMenuleri.forEach((menu) => {
        const tetikleyici = menu.querySelector("[data-ms-giris-menu-tetikleyici]");
        const girisPanel = menu.querySelector("[data-ms-giris-menu-panel]");
        const hesapPanelKapat = menu.querySelector("[data-ms-hesap-panel-kapat]");
        const desktopHoverMedya = window.matchMedia("(min-width: 1024px)");
        let hoverKapatTimer = null;

        if (!tetikleyici) {
            return;
        }

        const menuKapat = () => {
            if (hoverKapatTimer) {
                window.clearTimeout(hoverKapatTimer);
                hoverKapatTimer = null;
            }

            menu.classList.remove("ms-ana-navigasyon-giris-acik");
            tetikleyici.setAttribute("aria-expanded", "false");
        };

        const menuAc = () => {
            if (hoverKapatTimer) {
                window.clearTimeout(hoverKapatTimer);
                hoverKapatTimer = null;
            }

            menu.classList.add("ms-ana-navigasyon-giris-acik");
            tetikleyici.setAttribute("aria-expanded", "true");
        };

        const menuToggle = () => {
            const acik = menu.classList.toggle("ms-ana-navigasyon-giris-acik");
            tetikleyici.setAttribute("aria-expanded", acik ? "true" : "false");
        };

        tetikleyici.addEventListener("click", (event) => {
            event.preventDefault();
            menuToggle();
        });

        hesapPanelKapat?.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            menuKapat();
            tetikleyici.blur();
        });

        girisPanel?.addEventListener("pointerdown", (event) => {
            event.stopPropagation();
        });

        girisPanel?.addEventListener("click", (event) => {
            event.stopPropagation();
        });

        menu.addEventListener("pointerenter", () => {
            if (desktopHoverMedya.matches) {
                menuAc();
            }
        });

        menu.addEventListener("pointerleave", () => {
            if (!desktopHoverMedya.matches) {
                return;
            }

            hoverKapatTimer = window.setTimeout(menuKapat, 120);
        });

        window.addEventListener("scroll", menuKapat, { passive: true });

        document.addEventListener("pointerdown", (event) => {
            if (!menu.contains(event.target)) {
                menuKapat();
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                menuKapat();
                tetikleyici.blur();
            }
        });
    });

    if (!modal) {
        return;
    }

    const kodSayacYaz = () => {
        if (!kodSayac) {
            return;
        }

        const dakika = Math.floor(kodKalanSaniye / 60).toString().padStart(2, "0");
        const saniye = (kodKalanSaniye % 60).toString().padStart(2, "0");
        const sureDoldu = kodKalanSaniye <= 0;
        kodSayac.textContent = `${dakika}:${saniye}`;
        kodSayacAlani?.classList.toggle("ms-giris-kod-sayac-suresi-doldu", sureDoldu);

        if (kodYenidenGonder) {
            kodYenidenGonder.hidden = !sureDoldu;
        }

        if (kodOnayla) {
            kodOnayla.disabled = sureDoldu;
            kodOnayla.setAttribute("aria-disabled", sureDoldu.toString());
        }
    };

    const kodSayacDurdur = () => {
        if (kodSayacTimer) {
            window.clearInterval(kodSayacTimer);
            kodSayacTimer = null;
        }
    };

    const kodSayacBaslat = () => {
        kodSayacDurdur();
        kodKalanSaniye = 120;
        kodSayacYaz();

        kodSayacTimer = window.setInterval(() => {
            kodKalanSaniye = Math.max(0, kodKalanSaniye - 1);
            kodSayacYaz();

            if (kodKalanSaniye === 0) {
                kodSayacDurdur();
            }
        }, 1000);
    };

    const smsAdiminiGoster = (adim) => {
        if (!smsTelefonAdim || !smsKodAdim) {
            return;
        }

        smsTelefonAdim.classList.toggle("ms-giris-sms-adim-aktif", adim === "telefon");
        smsKodAdim.classList.toggle("ms-giris-sms-adim-aktif", adim === "kod");
        modal.classList.toggle("ms-giris-sms-kod-odak", adim === "kod");
        smsKodOdakDisiAlanlar.forEach((alan) => {
            alan.inert = adim === "kod";
        });

        if (adim === "telefon") {
            kodSayacDurdur();
            kodKalanSaniye = 120;
            kodSayacYaz();
        }
    };

    const tabAc = (hedef) => {
        tablar.forEach((tab) => {
            const aktif = tab.dataset.msGirisTab === hedef;
            tab.classList.toggle("ms-giris-tab-aktif", aktif);
            tab.setAttribute("aria-pressed", aktif ? "true" : "false");
        });

        paneller.forEach((panel) => {
            panel.classList.toggle("ms-giris-panel-aktif", panel.dataset.msGirisPanel === hedef);
        });

        if (hedef === "sms") {
            smsAdiminiGoster("telefon");
        } else {
            kodSayacDurdur();
        }
    };

    const modalAc = () => {
        sonOdaklananEleman = document.activeElement;
        modal.classList.add("ms-giris-modal-acik");
        modal.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";
        tabAc("sms");

        window.setTimeout(() => {
            const ilkGirdi = modal.querySelector(".ms-giris-panel-aktif input, .ms-giris-tab-aktif");
            ilkGirdi?.focus();
        }, 40);
    };

    const modalKapat = () => {
        kodSayacDurdur();
        modal.classList.remove("ms-giris-modal-acik");
        modal.setAttribute("aria-hidden", "true");
        if (!kayitModal?.classList.contains("ms-giris-modal-acik") && !belgeModalAcikMi()) {
            document.body.style.overflow = "";
        }
        sonOdaklananEleman?.focus?.();
    };

    const kayitModalAc = () => {
        sonOdaklananEleman = document.activeElement;
        modalKapat();
        girisMenuleri.forEach((menu) => {
            menu.classList.remove("ms-ana-navigasyon-giris-acik");
            menu.querySelector("[data-ms-giris-menu-tetikleyici]")?.setAttribute("aria-expanded", "false");
        });
        kayitModal?.classList.add("ms-giris-modal-acik");
        kayitModal?.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";

        window.setTimeout(() => {
            kayitModal?.querySelector("input, select, button")?.focus();
        }, 40);
    };

    const kayitModalKapat = () => {
        kayitModal?.classList.remove("ms-giris-modal-acik");
        kayitModal?.setAttribute("aria-hidden", "true");
        if (!modal.classList.contains("ms-giris-modal-acik") && !belgeModalAcikMi()) {
            document.body.style.overflow = "";
        }
        sonOdaklananEleman?.focus?.();
    };

    const navigasyonGirisPanelleriniKapat = () => {
        girisMenuleri.forEach((girisMenu) => {
            girisMenu.classList.remove("ms-ana-navigasyon-giris-acik");
            girisMenu.querySelector("[data-ms-giris-menu-tetikleyici]")?.setAttribute("aria-expanded", "false");
        });
    };

    const mobilPaneliKapat = () => {
        const mobilPanel = document.querySelector("[data-ms-mobil-menu]");
        const mobilMenuAcButonu = document.querySelector("[data-ms-mobil-menu-ac]");

        mobilPanel?.classList.remove("ms-ana-navigasyon-mobil-panel-acik");
        mobilPanel?.setAttribute("aria-hidden", "true");
        if (mobilPanel) {
            mobilPanel.inert = true;
        }
        mobilMenuAcButonu?.setAttribute("aria-expanded", "false");
    };

    document.addEventListener("click", (event) => {
        const girisButonu = event.target.closest("[data-ms-giris-modal-ac]");
        const kayitButonu = event.target.closest("[data-ms-kayit-modal-ac]");

        if (!girisButonu && !kayitButonu) {
            return;
        }

        const navigasyonPanelindenMi = event.target.closest("[data-ms-mobil-menu], [data-ms-giris-menu-panel]");

        if (!navigasyonPanelindenMi) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        mobilPaneliKapat();
        navigasyonGirisPanelleriniKapat();

        if (girisButonu) {
            modalAc();
            return;
        }

        kayitModalAc();
    }, true);

    const belgeModalAc = (baslik, belgeTuru, tetikleyici) => {
        if (belgeModalBaslik) {
            belgeModalBaslik.textContent = baslik || "Belge";
        }

        if (belgeModalIcerik) {
            belgeModalIcerik.innerHTML = belgeIcerikleri[belgeTuru] || "<p>İlgili bilgilendirme metni burada görüntülenir.</p>";
        }

        aktifBelgeCheckbox = tetikleyici?.closest(".ms-kayit-onay")?.querySelector('input[type="checkbox"]') || null;
        belgeModal?.classList.add("ms-ornek-modal-acik");
        belgeModal?.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";

        window.setTimeout(() => {
            belgeModal?.querySelector("[data-ms-belge-modal-kabul]")?.focus();
        }, 40);
    };

    const belgeModalKapat = () => {
        belgeModal?.classList.remove("ms-ornek-modal-acik");
        belgeModal?.setAttribute("aria-hidden", "true");
        if (!modal.classList.contains("ms-giris-modal-acik") && !kayitModal?.classList.contains("ms-giris-modal-acik")) {
            document.body.style.overflow = "";
        }
    };

    const belgeModalKabulEt = () => {
        if (aktifBelgeCheckbox) {
            aktifBelgeCheckbox.checked = true;
            aktifBelgeCheckbox.dispatchEvent(new Event("change", { bubbles: true }));
        }

        belgeModalKapat();
    };

    const telefonBicimlendir = (input) => {
        const rakamlar = input.value.replace(/\D/g, "").replace(/^0+/, "").slice(0, 10);
        input.value = [
            rakamlar.slice(0, 3),
            rakamlar.slice(3, 6),
            rakamlar.slice(6, 8),
            rakamlar.slice(8, 10)
        ].filter(Boolean).join(" ");
    };

    const oturumDurumuAyarla = (oturumAcik, acikKalacakMenu = null) => {
        girisMenuleri.forEach((menu) => {
            const tetikleyici = menu.querySelector("[data-ms-giris-menu-tetikleyici]");
            const yazi = menu.querySelector("[data-ms-giris-menu-yazi]");
            const acikKalacak = !oturumAcik && acikKalacakMenu === menu;

            menu.classList.toggle("ms-ana-navigasyon-giris-oturum-acik", oturumAcik);
            menu.classList.toggle("ms-ana-navigasyon-giris-acik", acikKalacak);
            tetikleyici?.setAttribute("aria-expanded", acikKalacak ? "true" : "false");

            if (yazi) {
                yazi.textContent = oturumAcik ? "Hesabım" : "Giriş Yap";
            }
        });
    };

    modalAcButonlari.forEach((buton) => {
        buton.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            girisMenuleri.forEach((menu) => {
                menu.classList.remove("ms-ana-navigasyon-giris-acik");
                menu.querySelector("[data-ms-giris-menu-tetikleyici]")?.setAttribute("aria-expanded", "false");
            });
            modalAc();
        });
    });

    kayitModalAcButonlari.forEach((buton) => {
        buton.addEventListener("click", (event) => {
            event.preventDefault();
            kayitModalAc();
        });
    });

    modalKapaticilar.forEach((kapatici) => {
        kapatici.addEventListener("click", modalKapat);
    });

    kayitModalKapaticilar.forEach((kapatici) => {
        kapatici.addEventListener("click", kayitModalKapat);
    });

    belgeModalAcButonlari.forEach((buton) => {
        buton.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            belgeModalAc(buton.dataset.baslik, buton.dataset.belgeTur, buton);
        });
    });

    belgeModalKapaticilar.forEach((kapatici) => {
        kapatici.addEventListener("click", belgeModalKapat);
    });

    belgeModalKabul?.addEventListener("click", belgeModalKabulEt);

    tablar.forEach((tab) => {
        tab.addEventListener("click", () => tabAc(tab.dataset.msGirisTab));
    });

    smsTelefon?.addEventListener("input", () => telefonBicimlendir(smsTelefon));

    [modal, kayitModal].forEach((modalAlani) => {
        modalAlani?.querySelectorAll('input[type="tel"]').forEach((input) => {
            input.addEventListener("input", () => telefonBicimlendir(input));
        });
    });

    kodGonder?.addEventListener("click", () => {
        const telefonRakamlari = smsTelefon?.value.replace(/\D/g, "") ?? "";

        if (telefonRakamlari.length < 10) {
            smsTelefon?.focus();
            return;
        }

        smsAdiminiGoster("kod");
        kodSayacBaslat();
        kodInputlari.forEach((input) => {
            input.value = "";
        });
        kodInputlari[0]?.focus();
    });

    smsGeri?.addEventListener("click", () => {
        smsAdiminiGoster("telefon");
        smsTelefon?.focus();
    });

    kodYenidenGonder?.addEventListener("click", () => {
        kodInputlari.forEach((input) => {
            input.value = "";
        });
        kodSayacBaslat();
        kodInputlari[0]?.focus();
    });

    kodOnayla?.addEventListener("click", () => {
        const girilenKod = kodInputlari.map((input) => input.value).join("");

        if (girilenKod.length < kodInputlari.length) {
            kodInputlari.find((input) => !input.value)?.focus();
            return;
        }

        oturumDurumuAyarla(true);
        modalKapat();
        window.setTimeout(() => {
            window.msOnayRedModalAc?.({
                tip: "onay",
                baslik: "Giriş Başarılı",
                altBaslik: "Giriş Yapıldı",
                metin: "Hesabınıza başarıyla giriş yaptınız.",
                sure: 1500
            });
        }, 40);
    });

    const kodTamamlandiysaOnayla = () => {
        const kodTamamlandi = kodInputlari.length > 0
            && kodInputlari.every((input) => input.value.length === 1);

        if (!kodTamamlandi || !kodOnayla || kodOnayla.disabled) {
            return;
        }

        window.setTimeout(() => kodOnayla.click(), 0);
    };

    kodInputlari.forEach((input, index) => {
        input.addEventListener("input", () => {
            input.value = input.value.replace(/\D/g, "").slice(0, 1);

            if (input.value && kodInputlari[index + 1]) {
                kodInputlari[index + 1].focus();
            }

            kodTamamlandiysaOnayla();
        });

        input.addEventListener("keydown", (event) => {
            if (event.key === "Backspace" && !input.value && kodInputlari[index - 1]) {
                kodInputlari[index - 1].focus();
            }
        });

        input.addEventListener("paste", (event) => {
            const yapistirilanKod = event.clipboardData.getData("text").replace(/\D/g, "").slice(0, kodInputlari.length);

            if (!yapistirilanKod) {
                return;
            }

            event.preventDefault();
            kodInputlari.forEach((kodInput, kodIndex) => {
                kodInput.value = yapistirilanKod[kodIndex] ?? "";
            });

            const odakIndex = Math.min(yapistirilanKod.length, kodInputlari.length) - 1;
            kodInputlari[Math.max(odakIndex, 0)]?.focus();
            kodTamamlandiysaOnayla();
        });
    });

    document.addEventListener("keydown", (event) => {
        if (event.key !== "Escape") {
            return;
        }

        if (belgeModalAcikMi()) {
            belgeModalKapat();
            return;
        }

        if (kayitModal?.classList.contains("ms-giris-modal-acik")) {
            kayitModalKapat();
            return;
        }

        if (modal.classList.contains("ms-giris-modal-acik")) {
            modalKapat();
        }
    });

    document.querySelectorAll("[data-ms-hesap-cikis]").forEach((cikisButonu) => {
        cikisButonu.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            oturumDurumuAyarla(false, cikisButonu.closest("[data-ms-giris-menu]"));
        });
    });
    };

    const girisEtkilesimiMi = (event) => event.target instanceof Element
        && Boolean(event.target.closest("[data-ms-giris-menu], [data-ms-giris-modal-ac], [data-ms-kayit-modal-ac]"));

    ["pointerdown", "focusin", "mouseover", "click"].forEach((eventAdi) => {
        document.addEventListener(eventAdi, (event) => {
            if (girisEtkilesimiMi(event)) {
                girisDavranislariniBaslat();
            }
        }, { capture: true, once: false });
    });
})();

// ─────────────────────────────────────────────────────────────────────────────
// İE-3 Takip köprüsü (msTakip, 2026-08-22 — docs/reklam-analytics-entegrasyon-is-akisi.md Faz C)
// window.ecspros (head partial _TakipBasligi.cshtml) varsa çalışır; yoksa (kanalda takip
// entegrasyonu yok / bot) hiçbir şey yapmaz. Üreticiler:
//  - sayfa JSON blokları: #ms-takip-urun (view_item + kalem kaydı), #ms-takip-liste (view_item_list/search)
//  - merkezi fetch gözlemcisi: sepet ekle/çıkar, favori, bülten, giriş/kayıt, checkout POST
//    (payment_info_added), /sepet|/teslimat|/odeme ilk sepet GET'i (cart_viewed/checkout_started/
//    shipping_info_added) — view'lara dokunmadan, başarı yanıtı üzerinden
//  - /siparis-tamamlandi: purchase (sessionStorage msSiparisSonucu, event_id = orderId, TEK SEFER)
// ─────────────────────────────────────────────────────────────────────────────
(function () {
    const E = () => window.ecspros;
    if (!E() || typeof E().track !== "function") { return; }
    const reg = E().urunler || (E().urunler = {});
    const trAd = (d) => (d && typeof d === "object") ? (d.tr || Object.values(d)[0] || null) : (d || null);
    const item = (o, ek) => Object.assign({
        item_id: o.sku || o.kod || o.id || "",
        item_group_id: o.kod || null,
        item_name: o.ad || o.kod || "",
        item_category: o.kategori || null,
        item_variant: o.varyant || null,
        price: o.fiyat != null ? o.fiyat : 0,
        quantity: o.adet || 1
    }, ek || {});
    const jsonOku = (id) => { try { const el = document.getElementById(id); return el ? JSON.parse(el.textContent) : null; } catch { return null; } };
    let bekle = null, sonSepet = null;
    const sepetKalemleri = (data) => ((data && data.items) || []).map((k) => item({
        sku: k.sku, kod: k.productCode, ad: trAd(k.productNameI18n) || k.productCode,
        fiyat: k.campaignUnitPrice != null ? k.campaignUnitPrice : k.addedPrice,
        varyant: k.optionsText, adet: k.quantity
    }, { discount: k.campaignLineDiscount || 0 }));

    function satinAlma() {
        let s = null;
        try { s = JSON.parse(sessionStorage.getItem("msSiparisSonucu") || "null"); } catch { /* yok */ }
        if (!s || !s.orderId) { return; }
        const anahtar = "msPurchaseSent:" + s.orderId;
        try { if (localStorage.getItem(anahtar)) { return; } } catch { /* depolama kapalı */ }
        const items = (s.kalemler || []).map((x) => item({
            sku: x.sku, kod: x.kod, ad: x.ad,
            fiyat: x.birimFiyat != null ? x.birimFiyat : (x.adet ? x.tutar / x.adet : x.tutar),
            varyant: x.secenek, adet: x.adet
        }, { discount: x.indirim || 0 }));
        E().track("order_completed", {
            event_id: s.orderId, transaction_id: s.orderNumber || s.orderId,
            value: s.odenecek, currency: "TRY", coupon: s.kuponKod || undefined,
            shipping: s.masraf || 0, items
        });
        try { localStorage.setItem(anahtar, "1"); } catch { /* depolama kapalı */ }
    }

    function sayfaBasi() {
        const u = jsonOku("ms-takip-urun");
        if (u) {
            const temel = { kod: u.kod, ad: u.ad, fiyat: u.fiyat, kategori: u.kategori, varyant: u.renk || null };
            reg[u.kod] = temel;
            (u.bedenler || []).forEach((b) => { reg[b.id] = Object.assign({}, temel, { fiyat: b.fiyat || u.fiyat, varyant: [u.renk, b.ad].filter(Boolean).join(", "), sku: b.sku || null }); });
            if (u.tekVaryant) { reg[u.tekVaryant] = temel; }
            E().track("product_viewed", { currency: u.paraBirimi || "TRY", value: u.fiyat, items: [item(temel)] });
        }
        const l = jsonOku("ms-takip-liste");
        if (l) {
            (l.urunler || []).forEach((p) => { reg[p.kod] = { kod: p.kod, ad: p.ad, fiyat: p.fiyat }; });
            const items = (l.urunler || []).map((p, i) => item(p, { item_list_id: l.listeId, index: i }));
            if (l.arama) { E().track("search", { search_term: l.arama, items: items.slice(0, 10) }); }
            E().track("product_list_viewed", { item_list_id: l.listeId, item_list_name: l.baslik, items });
        }
        const yol = location.pathname.toLowerCase().replace(/\/+$/, "");
        if (yol === "/siparis-tamamlandi") { satinAlma(); }
        else if (yol === "/sepet") { bekle = "cart_viewed"; }
        else if (yol === "/teslimat") { bekle = "checkout_started"; }
        else if (yol === "/odeme") { bekle = "shipping_info_added"; }
    }

    const govde = (b) => { try { return typeof b === "string" ? JSON.parse(b) : null; } catch { return null; } };
    function isle(url, metod, body, v) {
        if (!v || v.success === false) { return; }
        const yol = url.split("?")[0].replace(/^https?:\/\/[^/]+/, "");
        if (metod === "GET" && /\/api\/store\/cart$/.test(yol)) {
            sonSepet = v.data;
            if (bekle) { const b = bekle; bekle = null; E().track(b, { items: sepetKalemleri(v.data) }); }
            return;
        }
        if (metod === "POST" && /\/api\/store\/cart\/items$/.test(yol)) {
            const g = govde(body) || {};
            const r = reg[g.variantId] || { kod: "", ad: "", fiyat: g.price };
            E().track("added_to_cart", { items: [item(Object.assign({}, r, { adet: g.quantity || 1, fiyat: g.price || r.fiyat }))] });
            return;
        }
        if (metod === "DELETE" && /\/api\/store\/cart\/[^/]+\/items\/[^/]+$/.test(yol)) {
            const id = yol.split("/").pop();
            const k = sonSepet && sonSepet.items ? sonSepet.items.find((x) => x.id === id) : null;
            E().track("removed_from_cart", { items: k ? sepetKalemleri({ items: [k] }) : [] });
            return;
        }
        if (metod === "POST" && /\/api\/store\/favorites$/.test(yol)) {
            const g = govde(body) || {};
            const r = reg[g.productCode];
            E().track("wishlist_added", { items: r ? [item(r)] : [{ item_id: g.productCode || "", item_name: g.productCode || "" }] });
            return;
        }
        if (metod === "POST" && /\/api\/store\/newsletter$/.test(yol)) { E().track("newsletter_subscribed", {}); return; }
        if (metod === "POST" && /\/api\/store\/auth\/(login|otp\/verify)$/.test(yol)) { E().track("login", { method: /otp/.test(yol) ? "phone" : "email" }); return; }
        if (metod === "POST" && /\/api\/store\/auth\/register$/.test(yol)) { E().track("sign_up", { method: "email" }); return; }
        if (metod === "POST" && /\/api\/store\/checkout$/.test(yol)) {
            const g = govde(body) || {};
            E().track("payment_info_added", { payment_type: g.paymentMethod || "", items: sepetKalemleri(sonSepet) });
        }
    }
    // Fetch gözlemcisi — _Layout'taki token sarmalayıcının ÜSTÜNE (o /api/* token'ı ekler, biz yanıtı izleriz)
    const esasFetch = window.fetch;
    window.fetch = function (girdi, ayar) {
        const url = typeof girdi === "string" ? girdi : (girdi && girdi.url) || "";
        const metod = ((ayar && ayar.method) || (girdi && girdi.method) || "GET").toUpperCase();
        const sonuc = esasFetch.apply(this, arguments);
        if (url.indexOf("/api/store/") < 0 || url.indexOf("/api/store/events") >= 0) { return sonuc; }
        sonuc.then((y) => {
            if (!y || !y.ok) { return; }
            y.clone().json().then((v) => { try { isle(url, metod, ayar && ayar.body, v); } catch { /* takip hatası sayfayı etkilemez */ } }).catch(() => { });
        }).catch(() => { });
        return sonuc;
    };
    if (document.readyState === "loading") { document.addEventListener("DOMContentLoaded", sayfaBasi); } else { sayfaBasi(); }
})();
