# MySQL → PostgreSQL Ürün Grubu Eşleşme Tablosu

> Kız Çocuk / Erkek Çocuk / Bebek gibi MySQL kategorileri artık `Cinsiyet` + `Yaş Grubu`
> attribute kombinasyonuyla ifade edilir. "PG Cinsiyet" ve "PG Yaş Grubu" sütunları
> aktarımda hangi attribute değerlerinin atanacağını gösterir.

| MySQL ID | MySQL Kod | MySQL Açıklama | PG Grup Adı | PG Cinsiyet | PG Yaş Grubu | Durum | Ürün Sayısı |
|----------|-----------|----------------|-------------|-------------|--------------|-------|-------------|
| 1 | KE | Kadın Elbise | Elbise | Kadın | — | Eşleşti | 0 |
| 2 | KA | Kadın Aksesuar | Aksesuar | Kadın | — | Eşleşti | 0 |
| 3 | KP | Kadın Pantolon | Pantolon | Kadın | — | Eşleşti | 0 |
| 4 | KS | Kadin Spor | Bustiyer (grp_9) | Kadın | — | Birleştirildi† | 255 |
| 5 | KG | Kadin Gömlek | Gömlek | Kadın | — | Eşleşti | 0 |
| 6 | KB | Kadın Bluz | Bluz | Kadın | — | Eşleşti | 0 |
| 7 | KT | Kadın T-Shirt | T-Shirt | Kadın | — | Eşleşti | 0 |
| 9 | KY | Kadın Bustiyer | Bustiyer | Kadın | — | Eşleşti | 0 |
| 10 | ET | Kadın Etek | Etek | Kadın | — | Eşleşti | 0 |
| 11 | SW | Kadın Sweatshirt | Sweatshirt | Kadın | — | Eşleşti | 0 |
| 12 | HR | Kadın Hırka | Hırka | Kadın | — | Eşleşti | 0 |
| 13 | ES | Kadın Esofman | Eşofman (grp_47) | Kadın | — | Birleştirildi | 0 |
| 14 | TR | Kadın Triko | Triko | Kadın | — | Eşleşti | 0 |
| 15 | PJ | Kadın Pijama | Pijama | Kadın | — | Eşleşti | 0 |
| 16 | BL | Kadın Bolero | Bolero | Kadın | — | Eşleşti | 0 |
| 17 | YL | Kadın Yelek | Yelek | Kadın | — | Eşleşti | 0 |
| 18 | TN | Kadın Tunik | Tunik | Kadın | — | Eşleşti | 0 |
| 19 | TK | Kadın Takımlar | İkili Takım (grp_48) | Kadın | — | Birleştirildi† | 1734 |
| 20 | KS | Kadin Spor Ayakkabi | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 21 | BT | Kadın Bot | Bot | Kadın | — | Eşleşti | 0 |
| 22 | GA | Kadın Günlük Ayakkabı | Günlük Ayakkabı (gunluk_ayakkabi) | — | — | Birleştirildi | 0 |
| 23 | TA | Kadın Topuklu Ayakkabı | Topuklu Ayakkabı (topuklu_ayakkabi) | — | — | Birleştirildi | 0 |
| 24 | CZ | Kadın Çizme | Çizme | Kadın | — | Eşleşti | 0 |
| 25 | BB | Kadın Babet | Babet | Kadın | — | Eşleşti | 0 |
| 26 | TR | Kadın Peluş Ayakkabı Terlik | Peluş Terlik (pelus_terlik) | — | — | Birleştirildi | 0 |
| 27 | SD | Kadın Sandalet | Sandalet | Kadın | — | Eşleşti | 0 |
| 28 | EP | Erkek Spor Ayakkabi | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 29 | ES | Erkek Klasik Ayakkabı | Klasik Ayakkabı (klasik_ayakkabi) | — | — | Birleştirildi | 0 |
| 30 | EB | Erkek Bot | Bot (grp_21) | Erkek | — | Birleştirildi | 0 |
| 31 | EG | Erkek Günlük Ayakkabı | Günlük Ayakkabı (gunluk_ayakkabi) | — | — | Birleştirildi | 0 |
| 32 | BA | Erkek Büyük Beden Ayakkabi | — (grup kaldırıldı) | — | — | Kaldırıldı† — ürün yok | 0 |
| 33 | TR | Erkek Terlik | Terlik | Erkek | — | Eşleşti | 0 |
| 34 | CG | Erkek Çocuk Gömlek | Gömlek (grp_5) | Erkek | — | Birleştirildi | 0 |
| 35 | CT | Erkek Çocuk T-Shirt | T-Shirt (grp_7) | Erkek | — | Birleştirildi | 0 |
| 36 | TL | Erkek Çocuk Tulum | Tulum | Erkek | — | Eşleşti | 0 |
| 37 | CY | Erkek Çocuk Yelek | Yelek (grp_17) | Erkek | — | Birleştirildi | 0 |
| 38 | CS | Erkek Çocuk Sweatshirt | Sweatshirt (grp_11) | Erkek | — | Birleştirildi | 0 |
| 40 | TK | Erkek Çocuk Triko | Triko (grp_14) | Erkek | — | Birleştirildi | 0 |
| 41 | HR | Erkek Çocuk Hırka | Hırka (grp_12) | Erkek | — | Birleştirildi | 0 |
| 43 | CJ | Erkek Çocuk Pijama | Pijama (grp_15) | Erkek | — | Birleştirildi | 0 |
| 44 | BB | Erkek Çocuk Body | Body | — | — | Eşleşti | 0 |
| 45 | CP | Erkek Çocuk Pantolon | Pantolon (grp_3) | Erkek | — | Birleştirildi | 0 |
| 46 | CC | Erkek Çocuk Ceket | Ceket | Erkek | — | Eşleşti | 0 |
| 47 | ES | Erkek Çocuk Eşofman | Eşofman | Erkek | — | Eşleşti | 0 |
| 48 | TT | Erkek Çocuk İkili Takım | İkili Takım | Erkek | — | Eşleşti | 0 |
| 49 | CK | Erkek Çouk Kaban | Kaban (kaban) | — | — | Birleştirildi | 0 |
| 50 | CB | Erkek Çocuk Bot | Bot (grp_21) | Erkek | — | Birleştirildi | 0 |
| 51 | CA | Çocuk Spor Ayakkabı | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 52 | SA | Çocuk Terlik | Terlik (grp_33) | Unisex | — | Birleştirildi | 0 |
| 53 | SC | Çocuk Sandalet | Sandalet (grp_27) | Unisex | — | Birleştirildi | 0 |
| 54 | CM | Çocuk Çizme | Çizme (grp_24) | Unisex | — | Birleştirildi | 0 |
| 55 | AG | Çocuk Günlük Ayakkabı | Günlük Ayakkabı (gunluk_ayakkabi) | — | — | Birleştirildi | 0 |
| 56 | BC | Çocuk Babet | Babet (grp_25) | Unisex | — | Birleştirildi | 0 |
| 57 | EG | Erkek Gömlek | Gömlek (grp_5) | Erkek | — | Birleştirildi | 0 |
| 58 | ET | Erkek T-Shirt | T-Shirt (grp_7) | Erkek | — | Birleştirildi | 0 |
| 59 | EH | Erkek Hırka | Hırka (grp_12) | Erkek | — | Birleştirildi | 0 |
| 60 | EY | Erkek Yelek | Yelek (grp_17) | Erkek | — | Birleştirildi | 0 |
| 61 | EP | Erkek Pantolon | Pantolon (grp_3) | Erkek | — | Birleştirildi | 0 |
| 63 | TE | Erkek Takım Elbise | Takım Elbise | Erkek | — | Eşleşti | 0 |
| 64 | SE | Erkek Sweatshirt | Sweatshirt (grp_11) | Erkek | — | Birleştirildi | 0 |
| 65 | EE | Erkek Eşofman | Eşofman (grp_47) | Erkek | — | Birleştirildi | 0 |
| 67 | TT | Erkek Triko | Triko (grp_14) | Erkek | — | Birleştirildi | 0 |
| 68 | PP | Erkek Pijama | Pijama (grp_15) | Erkek | — | Birleştirildi | 0 |
| 69 | BE | Erkek Basic Body Atlet | Body (grp_44) | Erkek | — | Birleştirildi† | 87 |
| 70 | AT | Erkek Aktif Spor | Aktif Spor | Erkek | — | Eşleşti | 0 |
| 71 | AE | Erkek Aksesuar | Aksesuar (grp_2) | Erkek | — | Birleştirildi | 0 |
| 73 | EM | Erkek Mont | Mont | Erkek | — | Eşleşti | 0 |
| 75 | BK | Kadın Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 76 | CK | Kadın Ceket | Ceket (grp_46) | Kadın | — | Birleştirildi | 0 |
| 77 | PK | Kadın Kap | Kap | Kadın | — | Eşleşti | 0 |
| 78 | MK | Kadın Mont | Mont (grp_73) | Kadın | — | Birleştirildi | 0 |
| 80 | PP | Kadın Panço | Panço | Kadın | — | Eşleşti | 0 |
| 81 | HG | Kadın Hamile Giyim | Hamile Giyim (hamile_giyim) | — | — | Birleştirildi | 0 |
| 82 | SK | Kadın Sevgili Kombini | Sevgili Kombini (sevgili_kombini) | — | — | Birleştirildi | 0 |
| 83 | KC | Kadın Çanta | Çanta | Kadın | — | Eşleşti | 0 |
| 84 | CC | Kız Çocuk Çanta | Çanta (grp_83) | Kadın | — | Birleştirildi | 0 |
| 85 | CM | Kız Çocuk Mont | Mont (grp_73) | Kadın | — | Birleştirildi | 0 |
| 86 | AE | Kız Çocuk Elbise | Elbise (grp_1) | Kadın | — | Birleştirildi | 0 |
| 87 | CP | Kız Çocuk Pantolon | Pantolon (grp_3) | Kadın | — | Birleştirildi | 0 |
| 88 | BK | Kız Çocuk Bustiyer | Bustiyer (grp_9) | Kadın | — | Birleştirildi | 0 |
| 90 | KT | Kız Çocuk Tunik | Tunik (grp_18) | Kadın | — | Birleştirildi | 0 |
| 91 | BB | Kız Çocuk Bluz | Bluz (grp_6) | Kadın | — | Birleştirildi | 0 |
| 92 | KE | Kız Çocuk Etek | Etek (grp_10) | Kadın | — | Birleştirildi | 0 |
| 93 | KG | Kız Çocuk Gömlek | Gömlek (grp_5) | Kadın | — | Birleştirildi | 0 |
| 94 | TK | Kız Çocuk T-Shirt | T-Shirt (grp_7) | Kadın | — | Birleştirildi | 0 |
| 95 | KS | Kız Çocuk Şort | Şort | Kadın | — | Eşleşti | 0 |
| 96 | KP | Kız Çocuk Pijama | Pijama (grp_15) | Kadın | — | Birleştirildi | 0 |
| 97 | KB | Kız Çocuk Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 99 | EK | Kız Çocuk Eşofman | Eşofman (grp_47) | Kadın | — | Birleştirildi | 0 |
| 102 | CT | Kız Çocuk Triko | Triko (grp_14) | Kadın | — | Birleştirildi | 0 |
| 103 | KW | Kız Çocuk Sweatshirt | Sweatshirt (grp_11) | Kadın | — | Birleştirildi | 0 |
| 104 | KY | Kız Çocuk Yelek | Yelek (grp_17) | Kadın | — | Birleştirildi | 0 |
| 105 | HK | Kız Çocuk Hırka | Hırka (grp_12) | Kadın | — | Birleştirildi | 0 |
| 108 | CB | Kız Çocuk Bolero | Bolero (grp_16) | Kadın | — | Birleştirildi | 0 |
| 109 | ST | Kız Çocuk Tulum | Tulum (grp_36) | Kadın | — | Birleştirildi | 0 |
| 111 | TT | Kız Çocuk Takım | İkili Takım (grp_48) | Kadın | — | Birleştirildi† | 600 |
| 115 | AA | Kız Çocuk Aksesuar | Aksesuar (grp_2) | Kadın | — | Birleştirildi | 0 |
| 116 | CC | Erkek Çocuk Aksesuar | Aksesuar (grp_2) | Erkek | — | Birleştirildi | 0 |
| 117 | UA | Unisex Aksesuar | Aksesuar (grp_2) | Unisex | — | Birleştirildi | 0 |
| 118 | KI | Kadın İç Giyim | İç Giyim | Kadın | — | Eşleşti | 0 |
| 119 | EI | Erkek İç Giyim | İç Giyim (grp_118) | Erkek | — | Birleştirildi | 0 |
| 120 | CI | Erkek Çocuk İç Giyim | İç Giyim (grp_118) | Erkek | — | Birleştirildi | 0 |
| 121 | KI | Kız Çocuk İç Giyim | İç Giyim (grp_118) | Kadın | — | Birleştirildi | 0 |
| 123 | PG | Kadin Plaj Giyim | Plaj Giyim | Kadın | — | Eşleşti | 0 |
| 124 | PC | Kız Çocuk Plaj Giyim | Plaj Giyim (grp_123) | Kadın | — | Birleştirildi | 0 |
| 126 | BB | Kız Çocuk Babet | Babet (grp_25) | Kadın | — | Birleştirildi | 0 |
| 127 | BY | Kız Çocuk Bot | Bot (grp_21) | Kadın | — | Birleştirildi | 0 |
| 128 | GC | Kız Çocuk Günlük Ayakkabı | Günlük Ayakkabı (gunluk_ayakkabi) | — | — | Birleştirildi | 0 |
| 129 | KF | Kadın Klasik Ayakkabı | Klasik Ayakkabı (klasik_ayakkabi) | — | — | Birleştirildi | 0 |
| 130 | CE | Erkek Ceket | Ceket (grp_46) | Erkek | — | Birleştirildi | 0 |
| 131 | ME | Erkek Çocuk Mont | Mont (grp_73) | Erkek | — | Birleştirildi | 0 |
| 132 | KB | Kadın Kişisel Bakım | Kişisel Bakım | — | — | Eşleşti | 0 |
| 133 | TT | Kadın Tulum | Tulum (grp_36) | Kadın | — | Birleştirildi | 0 |
| 134 | CK | Kız Çocuk Çizme | Çizme (grp_24) | Kadın | — | Birleştirildi | 0 |
| 135 | BE | Bebek Eşofman | Eşofman (grp_47) | Unisex | — | Birleştirildi | 0 |
| 136 | BT | Çocuk Bot | Bot (grp_21) | Unisex | — | Birleştirildi | 0 |
| 137 | KZ | Kadın Makyaj Malzemeleri | Makyaj Malzemeleri | — | — | Eşleşti | 0 |
| 138 | EB | Erkek Kişisel Bakım | Kişisel Bakım (grp_132) | — | — | Birleştirildi | 0 |
| 141 | IT | Erkek İkili Takım | İkili Takım (grp_48) | Erkek | — | Birleştirildi | 0 |
| 142 | TE | Erkek Çocuk Takım Elbise | Takım Elbise (grp_63) | Erkek | — | Birleştirildi | 0 |
| 143 | EP | Erkek Çocuk Spor Ayakkabı | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 144 | TR | Erkek Çocuk Terlik | Terlik (grp_33) | Erkek | — | Birleştirildi | 0 |
| 145 | TE | Kız Çocuk Takım Elbise | Takım Elbise (grp_63) | Kadın | — | Birleştirildi | 0 |
| 146 | CK | Kız Çocuk Ceket | Ceket (grp_46) | Kadın | — | Birleştirildi | 0 |
| 147 | SA | Kız Çocuk Spor Ayakkabı | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 148 | TR | Kız Çocuk Terlik | Terlik (grp_33) | Kadın | — | Birleştirildi | 0 |
| 149 | KO | Kız Çocuk Kostüm | Kostüm | Kadın | — | Eşleşti | 0 |
| 150 | PT | Bebek Pantolon | Pantolon (grp_3) | Unisex | — | Birleştirildi | 0 |
| 151 | EL | Bebek Elbise | Elbise (grp_1) | Unisex | — | Birleştirildi | 0 |
| 152 | BL | Bebek Bluz | Bluz (grp_6) | Unisex | — | Birleştirildi | 0 |
| 153 | HR | Bebek Hırka | Hırka (grp_12) | Unisex | — | Birleştirildi | 0 |
| 154 | BD | Bebek Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 155 | PL | Bebek Plaj Giyim | Plaj Giyim (grp_123) | Unisex | — | Birleştirildi | 0 |
| 156 | PJ | Bebek Pijama | Pijama (grp_15) | Unisex | — | Birleştirildi | 0 |
| 157 | TK | Bebek Triko | Triko (grp_14) | Unisex | — | Birleştirildi | 0 |
| 158 | YL | Bebek Yelek | Yelek (grp_17) | Unisex | — | Birleştirildi | 0 |
| 159 | ZB | Bebek Zıbın | Zıbın | Unisex | Bebek | Eşleşti | 0 |
| 160 | TL | Bebek Tulum | Tulum (grp_36) | Unisex | — | Birleştirildi | 0 |
| 161 | IT | Bebek İkili Takım | İkili Takım (grp_48) | Unisex | — | Birleştirildi | 0 |
| 162 | IC | Bebek İç Giyim | İç Giyim (grp_118) | Unisex | — | Birleştirildi | 0 |
| 163 | TS | Bebek T-Shirt | T-Shirt (grp_7) | Unisex | — | Birleştirildi | 0 |
| 164 | SW | Bebek Sweatshirt | Sweatshirt (grp_11) | Unisex | — | Birleştirildi | 0 |
| 165 | BY | Erkek Banyo Giyim | Banyo Giyim | — | — | Eşleşti | 0 |
| 166 | BY | Ekrek Çocuk Banyo Giyim | Banyo Giyim (grp_165) | — | — | Birleştirildi | 0 |
| 167 | BY | Kız Çocuk Banyo Giyim | Banyo Giyim (grp_165) | — | — | Birleştirildi | 0 |
| 169 | BY | Kadın Banyo Giyim | Banyo Giyim (grp_165) | — | — | Birleştirildi | 0 |
| 171 | FR | Kadın Farace | Farace | — | — | Eşleşti | 0 |
| 173 | KM | Kadın Kostüm | Kostüm (grp_149) | Kadın | — | Birleştirildi | 0 |
| 174 | SL | Kadin Şal | Şal | Kadın | — | Eşleşti | 0 |
| 175 | BT | Kız Bebek Pantolon | Pantolon (grp_3) | Kadın | — | Birleştirildi | 0 |
| 176 | TA | Telefon ve Aksesuarları | Telefon ve Aksesuarları | — | — | Eşleşti | 0 |
| 177 | BL | Bilgisayar | Bilgisayar | — | — | Eşleşti | 0 |
| 178 | TL | Televizyon ve Aksesuar | Televizyon ve Aksesuar | — | — | Eşleşti | 0 |
| 179 | EA | Elektirikli Ev Aletleri | Elektirikli Ev Aletleri | — | — | Eşleşti | 0 |
| 180 | BY | Beyaz Eşya | Beyaz Eşya | — | — | Eşleşti | 0 |
| 181 | MB | Mobilyalar | Mobilyalar | — | — | Eşleşti | 0 |
| 182 | DA | Dekorasyon ve Aydınlatma | Dekorasyon ve Aydınlatma | — | — | Eşleşti | 0 |
| 183 | ET | Ev Tekstil | Ev Tekstil | — | — | Eşleşti | 0 |
| 184 | MT | Mutfak Gereçleri | Mutfak Gereçleri | — | — | Eşleşti | 0 |
| 185 | BN | Banyo ve Ev Gereçleri | Banyo ve Ev Gereçleri | — | — | Eşleşti | 0 |
| 186 | CZ | Kadin Çeyiz Setleri | Çeyiz Setleri | — | — | Eşleşti | 0 |
| 187 | BG | Unisex Banyo Giyim | Banyo Giyim (grp_165) | — | — | Birleştirildi | 0 |
| 188 | UK | Unisex Kostüm | Kostüm (grp_149) | Unisex | — | Birleştirildi | 0 |
| 189 | UL | Unisex İç Giyim | İç Giyim (grp_118) | Unisex | — | Birleştirildi | 0 |
| 190 | UT | Unisex Tulum | Tulum (grp_36) | Unisex | — | Birleştirildi | 0 |
| 191 | UM | Unisex Makyaj Ürünleri | Banyo ve Ev Gereçleri (grp_185) | — | — | Birleştirildi‡ | 6 |
| 192 | UT | Unisex Peluş Ayakkabı Terlik | Peluş Terlik (pelus_terlik) | — | — | Birleştirildi | 0 |
| 193 | US | Unisex Spor Ayakkabi | Spor Ayakkabı (spor_ayakkabi) | — | — | Birleştirildi | 0 |
| 194 | UB | Unisex Bot | Bot (grp_21) | Unisex | — | Birleştirildi | 0 |
| 195 | CZ | Unisex Çizme | Çizme (grp_24) | Unisex | — | Birleştirildi | 0 |
| 197 | KB | Unisex Kişisel Bakım | Kişisel Bakım (grp_132) | — | — | Birleştirildi | 0 |
| 198 | KK | Kadın Kimono | Kimono | Kadın | — | Eşleşti | 0 |
| 199 | EP | Erkek Çocuk Plaj Giyim | Plaj Giyim (grp_123) | Erkek | — | Birleştirildi | 0 |
| 201 | BL | Kız Bebek Bluz | Bluz (grp_6) | Kadın | — | Birleştirildi | 0 |
| 202 | BD | Kız Bebek Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 203 | EL | Kız Bebek Elbise | Elbise (grp_1) | Kadın | — | Birleştirildi | 0 |
| 204 | BE | Kız Bebek Eşofman | Eşofman (grp_47) | Kadın | — | Birleştirildi | 0 |
| 205 | HR | Kız Bebek Hırka | Hırka (grp_12) | Kadın | — | Birleştirildi | 0 |
| 206 | IC | Kız Bebek İç Giyim | İç Giyim (grp_118) | Kadın | — | Birleştirildi | 0 |
| 207 | IT | Kız Bebek İkili Takım | İkili Takım (grp_48) | Kadın | — | Birleştirildi | 0 |
| 208 | PJ | Kız Bebek Pijama | Pijama (grp_15) | Kadın | — | Birleştirildi | 0 |
| 209 | PL | Kız Bebek Plaj Giyim | Plaj Giyim (grp_123) | Kadın | — | Birleştirildi | 0 |
| 210 | SW | Kız Bebek Sweatshirt | Sweatshirt (grp_11) | Kadın | — | Birleştirildi | 0 |
| 211 | TS | Kız Bebek T-Shirt | T-Shirt (grp_7) | Kadın | — | Birleştirildi | 0 |
| 212 | TK | Kız Bebek Triko | Triko (grp_14) | Kadın | — | Birleştirildi | 0 |
| 213 | TL | Kız Bebek Tulum | Tulum (grp_36) | Kadın | — | Birleştirildi | 0 |
| 214 | YL | Kız Bebek Yelek | Yelek (grp_17) | Kadın | — | Birleştirildi | 0 |
| 215 | ZB | Kız Bebek Zıbın | Zıbın (grp_159) | Kadın | Bebek | Birleştirildi | 0 |
| 216 | BL | Erkek Bebek Bluz | Bluz (grp_6) | Erkek | — | Birleştirildi | 0 |
| 217 | BD | Erkek Bebek Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 218 | EL | Erkek Bebek Elbise | Elbise (grp_1) | Erkek | — | Birleştirildi | 0 |
| 219 | BE | Erkek Bebek Eşofman | Eşofman (grp_47) | Erkek | — | Birleştirildi | 0 |
| 220 | HR | Erkek Bebek Hırka | Hırka (grp_12) | Erkek | — | Birleştirildi | 0 |
| 221 | IC | Erkek Bebek İç Giyim | İç Giyim (grp_118) | Erkek | — | Birleştirildi | 0 |
| 222 | IT | Erkek Bebek İkili Takım | İkili Takım (grp_48) | Erkek | — | Birleştirildi | 0 |
| 223 | BT | Erkek Bebek Pantolon | Pantolon (grp_3) | Erkek | — | Birleştirildi | 0 |
| 224 | PJ | Erkek Bebek Pijama | Pijama (grp_15) | Erkek | — | Birleştirildi | 0 |
| 225 | PL | Erkek Bebek Plaj Giyim | Plaj Giyim (grp_123) | Erkek | — | Birleştirildi | 0 |
| 226 | SW | Erkek Bebek Sweatshirt | Sweatshirt (grp_11) | Erkek | — | Birleştirildi | 0 |
| 227 | TS | Erkek Bebek T-Shirt | T-Shirt (grp_7) | Erkek | — | Birleştirildi | 0 |
| 228 | TK | Erkek Bebek Triko | Triko (grp_14) | Erkek | — | Birleştirildi | 0 |
| 229 | TL | Erkek Bebek Tulum | Tulum (grp_36) | Erkek | — | Birleştirildi | 0 |
| 230 | YL | Erkek Bebek Yelek | Yelek (grp_17) | Erkek | — | Birleştirildi | 0 |
| 231 | ZB | Erkek Bebek Zıbın | Zıbın (grp_159) | Erkek | Bebek | Birleştirildi | 0 |
| 248 | EG | Erkek Bebek Gömlek | Gömlek (grp_5) | Erkek | — | Birleştirildi | 0 |
| 249 | KE | Kız Bebek Etek | Etek (grp_10) | Kadın | — | Birleştirildi | 0 |
| 250 | BD | Erkek Bodyler | Body (grp_44) | — | — | Birleştirildi | 0 |
| 251 | CM | Çocuk Mont | Mont (grp_73) | Unisex | — | Birleştirildi | 0 |
| 252 | EM | Erkek Bebek Mont | Mont (grp_73) | Erkek | — | Birleştirildi | 0 |
| 254 | 1 | Çocuk Kapri | Kapri | Unisex | — | Eşleşti | 0 |
| 255 | CE | Çocuk Eşofman | Eşofman (grp_47) | Unisex | — | Birleştirildi | 0 |
| 256 | CT | Çocuk T-Shirt | T-Shirt (grp_7) | Unisex | — | Birleştirildi | 0 |
| 257 | CP | Çocuk Pantolon  | Pantolon (grp_3) | Unisex | — | Birleştirildi | 0 |
| 258 | CS | Çocuk Sweatshirt | Sweatshirt (grp_11) | Unisex | — | Birleştirildi | 0 |
| 259 | CB | Çocuk Body | Body (grp_44) | — | — | Birleştirildi | 0 |
| 262 | TC | Erkek Trençkot | Trençkot | Erkek | — | Eşleşti | 0 |
| 269 | KL | Kulaklık | Kulaklık | — | — | Eşleşti | 0 |

**Toplam MySQL grubu:** 217
**Eşleşti (bire-bir):** 56
**Birleştirildi (duplicate):** 160
**Kaldırıldı (ürünsüz grup):** 1
**Eşleşmedi:** 0
**PG'deki toplam grup:** 71 (bu dosyanın önceki hâlindeki 79 sayısı, bu oturumdan önce ayrıca
kaldırılan `grp_171`/Farace'ı henüz yansıtmıyordu — güncel gerçek sayı canlı DB'den doğrulandı)

---

## 2026-07-01 — Yeniden eşleme (7 grup fiziksel silindi)

Aşağıdaki 7 PG grubu `definition.product_groups` tablosundan fiziksel olarak silindi ve
seed kodundan (`DatabaseSeeder.cs`) çıkarıldı: `Spor` (grp_4), `Takımlar` (grp_19),
`Takım` (grp_111), `Makyaj Ürünleri` (grp_191), `Basic Body` (basic_body),
`Büyük Beden Ayakkabı` (buyuk_beden_ayakkabi), `Ceket/Mont` (jacket — hiçbir MySQL
grubu buna eşlenmemişti, tamamen kullanılmayan sentetik bir kayıttı).

† Bu 5 MySQL grubu daha önce yukarıdaki gruplardan birine eşleniyordu; ürün adları
MySQL'den tekrar örneklenerek en yakın kalan gruba yeniden yönlendirildi:
- **Id 4 (Kadin Spor, 255 ürün):** örnek ürün adları ağırlıklı "Sporcu Büstiyer" /
  "Atlet" — `Bustiyer` (grp_9) ile eşleştirildi.
- **Id 19 (Kadın Takımlar, 1734 ürün)** ve **Id 111 (Kız Çocuk Takım, 600 ürün):**
  örnek ürün adlarının neredeyse tamamı "İkili Takım" ibaresi içeriyor —
  `İkili Takım` (grp_48) ile eşleştirildi.
- **Id 32 (Erkek Büyük Beden Ayakkabi):** MySQL'de **0 ürün** — grup tamamen boş,
  yeniden yönlendirmeye gerek yok, kaldırıldı olarak işaretlendi.
- **Id 69 (Erkek Basic Body Atlet, 87 ürün):** örnek ürün adları "Bisiklet/Balıkçı
  Yaka Basic Body", "Termal Fanila" — `Body` (grp_44) ile eşleştirildi.

‡ **Id 191 (Unisex Makyaj Ürünleri, 6 ürün):** örnek ürün adları incelendiğinde bunların
aslında makyaj değil, **ev düzenleyici/organizer ürünleri** olduğu görüldü (ör. "Çorap
Düzenleyici", "Buz Dolabı Organizeri", "Dolap İçi Saklama Sepeti") — MySQL tarafında
yanlış kategorize edilmiş. `Banyo ve Ev Gereçleri` (grp_185) ile eşleştirildi.
