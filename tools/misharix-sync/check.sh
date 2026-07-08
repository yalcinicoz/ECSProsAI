#!/usr/bin/env bash
# Misharix tasarım kaynağı ↔ ECSPros storefront drift kontrolü (plan A7).
#
# Kural: partial'lar bayt-bayt aynı olmalı; bilinçli farklar yalnızca
#   - Razor data-binding satırları (@model, @Model., @foreach, @if vb.)
#   - allowed-diffs.txt'te listelenen dosyalar (gerekçesiyle)
# Kullanım: tools/misharix-sync/check.sh   (çıkış kodu 0 = temiz)

set -u
KAYNAK="/opt/misharixWebSites/misharix"
HEDEF="/opt/ECSProsAI/src/ECSPros.Api"
BURASI="$(cd "$(dirname "$0")" && pwd)"
IZINLI="$BURASI/allowed-diffs.txt"
sorun=0

kontrol() { # $1: kaynak dosya, $2: hedef dosya, $3: görünen ad
  if [ ! -f "$2" ]; then return 0; fi                      # henüz taşınmamış — sorun değil
  if [ ! -f "$1" ]; then
    if grep -qxF "$3" "$IZINLI" 2>/dev/null; then
      echo "İZİNLİ YENİ : $3"                              # kaynakta karşılığı olmayan bilinçli dosya
      return 0
    fi
    echo "KAYNAKTA YOK: $3"; sorun=1; return
  fi
  if cmp -s "$1" "$2"; then return 0; fi
  if grep -qxF "$3" "$IZINLI" 2>/dev/null; then
    echo "İZİNLİ FARK : $3"
    return 0
  fi
  echo "BEKLENMEYEN FARK: $3"
  diff "$1" "$2" | head -20 | sed 's/^/    /'
  sorun=1
}

# Views ağacı: hedefte var olan her cshtml kaynaktakiyle karşılaştırılır
while IFS= read -r hedefDosya; do
  rel="${hedefDosya#"$HEDEF"/Views/}"
  kontrol "$KAYNAK/Views/$rel" "$hedefDosya" "Views/$rel"
done < <(find "$HEDEF/Views" -name "*.cshtml" \
           ! -name "_ViewImports.cshtml" ! -name "_ViewStart.cshtml" \
           ! -name "_MsTemaTokenlari.cshtml" ! -path "*/Themes/*" 2>/dev/null)

# CSS / JS
kontrol "$KAYNAK/wwwroot/css/tailwind.css" "$HEDEF/wwwroot/css/tailwind.css" "wwwroot/css/tailwind.css"
kontrol "$KAYNAK/wwwroot/js/site.js"       "$HEDEF/wwwroot/js/site.js"       "wwwroot/js/site.js"

if [ "$sorun" -eq 0 ]; then
  echo "DRIFT KONTROLÜ TEMİZ ✓ (hedefteki her dosya kaynakla aynı ya da izinli listede)"
else
  echo "DRIFT VAR ✗ — beklenmeyen farklar yukarıda. Ya kaynaktan yeniden kopyala ya da gerekçesiyle allowed-diffs.txt'e ekle."
fi
exit "$sorun"
