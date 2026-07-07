// Misharix global UI davranislari.
// Bu dosyada veri uretimi veya sayfaya ozel fetch/listeleme mantigi tutulmaz.

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

    const kartTiklamasiniHazirla = (kart) => {
        if (!kart || kart.dataset.msKartLinkHazir === "true") {
            return;
        }

        kart.dataset.msKartLinkHazir = "true";
        kart.addEventListener("click", (event) => {
            if (event.target.closest("a, button, input, select, textarea, [role='button'], [data-ms-kart-link-yoksay], [data-ms-urun-video], .ms-urun-video-alani, .ms-urun-renk-tooltip-alani, .ms-urun-renk-rozet")) {
                return;
            }

            kart.querySelector("[data-ms-kart-link]")?.click();
        });
    };

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
        placeholder.innerHTML = "<span>Placeholder</span>";

        const skeleton = document.createElement("span");
        skeleton.className = "ms-lazy-skeleton";
        skeleton.setAttribute("aria-hidden", "true");

        kapsayici.appendChild(placeholder);
        kapsayici.appendChild(skeleton);
    };

    const gorselYukle = (img) => {
        const lazySrc = img.dataset.msLazySrc;
        const lazySrcset = img.dataset.msLazySrcset;
        const lazySizes = img.dataset.msLazySizes;

        if (!lazySrc && !lazySrcset) {
            img.classList.add("ms-lazy-gorsel-yuklendi");
            return;
        }

        img.addEventListener("load", () => {
            img.classList.add("ms-lazy-gorsel-yuklendi");
        }, { once: true });

        if (lazySizes) {
            img.sizes = lazySizes;
        }

        if (lazySrcset) {
            img.srcset = lazySrcset;
        }

        if (lazySrc) {
            img.src = lazySrc;
        }

        img.removeAttribute("data-ms-lazy-src");
        img.removeAttribute("data-ms-lazy-srcset");
        img.removeAttribute("data-ms-lazy-sizes");
    };

    const gorselHazirla = (img) => {
        if (!(img instanceof HTMLImageElement) || !lazyInfiniteAktifMi(img) || img.dataset.msLazyHazir === "true" || img.dataset.msLazy === "false" || img.classList.contains("no-lazy")) {
            return;
        }

        img.dataset.msLazyHazir = "true";

        if (!img.hasAttribute("loading")) {
            img.loading = "lazy";
        }

        if (!img.hasAttribute("decoding")) {
            img.decoding = "async";
        }

        if (!img.dataset.msLazySrc && !img.dataset.msLazySrcset) {
            return;
        }

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
        lazyLoadYenile();

        if ("MutationObserver" in window) {
            const mutationObserver = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
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
            const ozelSelectler = document.querySelectorAll("[data-ms-ozel-select]");
            const telefonUlkeSelectleri = document.querySelectorAll("[data-ms-telefon-ulke-select]");
            const telefonInputlari = document.querySelectorAll("[data-ms-telefon-input]");
            const kodGirisleri = document.querySelectorAll("[data-ms-kod-giris]");
            const kodDetaylari = document.querySelectorAll("[data-code-detail]");
            const ornekModalAcButonlari = document.querySelectorAll("[data-ms-ornek-modal-ac]");
            const ornekModallar = document.querySelectorAll("[data-ms-ornek-modal]");
            const ornekModalBoyutClasslari = ["ms-ornek-modal-boyut-m", "ms-ornek-modal-boyut-l", "ms-ornek-modal-boyut-xl", "ms-ornek-modal-boyut-2xl"];
            const boyutClasslari = ["ms-buton-x", "ms-buton-s", "ms-buton-m", "ms-buton-l", "ms-buton-xl", "ms-buton-xxl"];
            const varsayilanProjeSekmesi = projeKok?.dataset.msProjeVarsayilanSekme || projeKapsam.querySelector(".ms-sekme-aktif")?.dataset.tab || "gorunum-tipleri";
            const arayuzTablari = new Set(["butonlar", "filtreler", "rozetler", "ikons", "bildirimler", "modallar"]);
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
                    let surukleniyor = false;
                    let suruklemeYapildi = false;
                    let baslangicX = 0;
                    let baslangicScroll = 0;
                    let tiklamaEngellenecek = false;

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

                    const guncelle = () => {
                        const kartlar = kartlariAl();
                        const kaydirilabilir = liste.scrollWidth > liste.clientWidth + 2;
                        const basta = liste.scrollLeft <= 1;
                        const sonda = liste.scrollLeft + liste.clientWidth >= liste.scrollWidth - 1;

                        solKontrol?.toggleAttribute("disabled", !kaydirilabilir || basta);
                        sagKontrol?.toggleAttribute("disabled", !kaydirilabilir || sonda);

                        if (sayac && kartlar.length > 0) {
                            sayac.textContent = `${aktifKartIndexiniBul() + 1} / ${kartlar.length}`;
                        }
                    };

                    carousel.msGorunumCarouselGuncelle = () => window.requestAnimationFrame(guncelle);

                    const kaydir = (yon) => {
                        const yonCarpani = yon === "sag" ? 1 : -1;
                        kartaGit(aktifKartIndexiniBul() + yonCarpani);
                    };

                    solKontrol?.addEventListener("click", () => kaydir("sol"));
                    sagKontrol?.addEventListener("click", () => kaydir("sag"));
                    liste.addEventListener("scroll", guncelle, { passive: true });
                    liste.addEventListener("dragstart", (event) => event.preventDefault());
                    liste.addEventListener("click", (event) => {
                        if (tiklamaEngellenecek) {
                            event.preventDefault();
                            tiklamaEngellenecek = false;
                        }
                    });
                    liste.addEventListener("pointerdown", (event) => {
                        if (event.button !== undefined && event.button !== 0) {
                            return;
                        }

                        surukleniyor = true;
                        suruklemeYapildi = false;
                        tiklamaEngellenecek = false;
                        baslangicX = event.clientX;
                        baslangicScroll = liste.scrollLeft;
                        liste.classList.add("ms-gorunum-carousel-surukleniyor");
                        liste.setPointerCapture?.(event.pointerId);
                    });
                    liste.addEventListener("pointermove", (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        const fark = event.clientX - baslangicX;

                        if (Math.abs(fark) > 6) {
                            suruklemeYapildi = true;
                            tiklamaEngellenecek = true;
                            event.preventDefault();
                        }

                        liste.scrollLeft = baslangicScroll - fark;
                        guncelle();
                    });

                    const suruklemeyiBitir = (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        const hizalanacakIndex = suruklemeYapildi ? aktifKartIndexiniBul() : -1;
                        surukleniyor = false;
                        liste.classList.remove("ms-gorunum-carousel-surukleniyor");

                        if (typeof event.pointerId === "number" && liste.hasPointerCapture?.(event.pointerId)) {
                            liste.releasePointerCapture(event.pointerId);
                        }

                        if (hizalanacakIndex >= 0) {
                            kartaGit(hizalanacakIndex);
                        } else {
                            guncelle();
                        }
                    };

                    liste.addEventListener("pointerup", suruklemeyiBitir);
                    liste.addEventListener("pointercancel", suruklemeyiBitir);
                    liste.addEventListener("mouseleave", suruklemeyiBitir);
                    window.addEventListener("resize", guncelle);
                    window.requestAnimationFrame(guncelle);
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
                        liste.setPointerCapture?.(event.pointerId);
                    });
                    liste.addEventListener("pointermove", (event) => {
                        if (!surukleniyor) {
                            return;
                        }

                        event.preventDefault();
                        if (Math.abs(event.clientX - baslangicX) > 6) {
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

                    link.click();
                });
            });

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

                const modalAc = () => {
                    sonOdaklananEleman = document.activeElement;
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
                    buton.addEventListener("click", modalAc);
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

            window.msKoleksiyonModallariBaslat = koleksiyonModallariBaslat;
            koleksiyonModallariBaslat();

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

                            kart.querySelector("[data-ms-kart-link]")?.click();
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
                    buton.setAttribute("aria-expanded", (!acik).toString());
                    icerik.classList.toggle("ms-gizli", acik);
                    ok.classList.toggle("ms-filtre-ok-acik", !acik);
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

            ozelSelectler.forEach((select) => {
                const tetikleyici = select.querySelector("[data-ms-ozel-select-tetikleyici]");
                const deger = select.querySelector("[data-ms-ozel-select-deger]");
                const secenekler = select.querySelectorAll("[data-ms-ozel-select-secenek]");
                const arama = select.querySelector("[data-ms-ozel-select-arama]");
                const coklu = select.hasAttribute("data-ms-ozel-select-coklu");
                const checkboxli = select.hasAttribute("data-ms-ozel-select-checkboxli");
                const temizleButonu = select.querySelector("[data-ms-ozel-select-temizle]");
                const uygulaButonu = select.querySelector("[data-ms-ozel-select-uygula]");
                const sayac = select.querySelector("[data-ms-ozel-select-sayac]");

                if (!tetikleyici || !deger) {
                    return;
                }

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

                const kapat = () => {
                    select.classList.remove("ms-ozel-select-acik");
                    tetikleyici.setAttribute("aria-expanded", "false");
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

            telefonUlkeSelectleri.forEach((select) => {
                const tetikleyici = select.querySelector("[data-ms-telefon-ulke-tetikleyici]");
                const kod = select.querySelector("[data-ms-telefon-ulke-kod]");
                const bayrak = select.querySelector(".ms-telefon-ulke-tetikleyici .ms-telefon-bayrak");
                const arama = select.querySelector("[data-ms-telefon-ulke-arama]");
                const secenekler = select.querySelectorAll("[data-ms-telefon-ulke-secenek]");

                if (!tetikleyici || !kod || !bayrak) {
                    return;
                }

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

                        secenekler.forEach((oge) => {
                            oge.classList.toggle("ms-telefon-ulke-secenek-aktif", oge === secenek);
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

            telefonInputlari.forEach((input) => {
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

            gorselAlani.addEventListener("pointerdown", (event) => {
                if (gecisYapiliyor || (event.button !== undefined && event.button !== 0)) {
                    return;
                }

                surukleniyor = true;
                tiklamaEngellenecek = false;
                suruklemeBaslangicX = event.clientX;
                suruklemeFarki = 0;
                gorselAlani.classList.add("ms-slider-gorsel-alani-surukleniyor");
                gorselAlani.setPointerCapture?.(event.pointerId);
                otomatikDurdur();
            });

            gorselAlani.addEventListener("pointermove", (event) => {
                if (!surukleniyor) {
                    return;
                }

                suruklemeFarki = event.clientX - suruklemeBaslangicX;
                const yon = suruklemeFarki < 0 ? -1 : 1;

                if (Math.abs(suruklemeFarki) > 6) {
                    tiklamaEngellenecek = true;
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

                const esik = Math.max(60, gorselAlani.clientWidth * 0.12);
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
                    durumGuncelle(!buton.classList.contains("ms-urun-koleksiyon-aktif"));
                });
            });
        };

        const renkTooltipHazirla = (kok) => {
            const renkTooltipDurumunuGuncelle = () => {
                const acikKartVar = Boolean(document.querySelector(".ms-urun-karti.ms-urun-renk-tooltip-acik"));
                document.documentElement.classList.toggle("ms-urun-renk-tooltip-body-kilitli", acikKartVar);
                document.body.classList.toggle("ms-urun-renk-tooltip-body-kilitli", acikKartVar);
            };

            const renkTooltipKapat = (kart) => {
                kart?.classList.remove("ms-urun-renk-tooltip-acik");
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
            let hikayeler = [];
            let aktifIndex = 0;
            let baslangic = 0;
            let gecenSure = 0;
            let animasyonKaresi = 0;
            let duraklatildi = false;
            let modalAcik = false;
            let storyBasili = false;
            let storyBasiliDuraklatildi = false;
            let storyBasiliOnceDuraklatildi = false;
            let storyBasiliZamanlayici = 0;
            const mobilStoryEslesmesi = window.matchMedia("(max-width: 639px)");

            if (!modal || !cerceve || !modalGorsel || !modalVideo || Object.keys(storyGruplari).length === 0) {
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

            const ilerlet = (zaman) => {
                if (!modalAcik || duraklatildi) {
                    return;
                }

                if (!baslangic) {
                    baslangic = zaman - gecenSure;
                }

                gecenSure = zaman - baslangic;
                const oran = Math.min(1, gecenSure / storySuresi);
                progressleriGuncelle(oran);

                if (oran >= 1) {
                    goster(aktifIndex + 1);
                    return;
                }

                animasyonKaresi = window.requestAnimationFrame(ilerlet);
            };

            const aktifVideoMu = () => !modalVideo.classList.contains("ms-gizli");

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
                aktifIndex = (index + hikayeler.length) % hikayeler.length;
                const hikaye = hikayeler[aktifIndex];
                const videoMu = hikaye.tip === "video";

                modalGorsel.classList.toggle("ms-gizli", videoMu);
                modalVideo.classList.toggle("ms-gizli", !videoMu);
                modalVideo.pause();

                if (videoMu) {
                    modalVideo.src = hikaye.url;
                    modalVideo.setAttribute("aria-label", `${hikaye.baslik} story videosu`);
                    modalGorsel.removeAttribute("src");
                    modalGorsel.alt = "";
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
                hikayeler = storyGruplari[grupAdi] || [];

                if (!hikayeler.length) {
                    return;
                }

                modalAcik = true;
                modal.classList.remove("ms-gizli");
                modal.setAttribute("aria-hidden", "false");
                document.body.classList.add("ms-story-modal-acik");
                progressleriOlustur();
                goster(0);
            };

            const kapat = () => {
                modalAcik = false;
                window.cancelAnimationFrame(animasyonKaresi);
                modal.classList.add("ms-gizli");
                modal.setAttribute("aria-hidden", "true");
                modalVideo.pause();
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
