#!/bin/bash
# Faz 1: Data Protection anahtar halkası yedeği (docs/dayaniklilik-faz0-plani.md).
# Bu anahtarlar kaybolursa DB'deki şifreli entegrasyon kimlik bilgileri ÇÖZÜLEMEZ.
set -e
SRC="$HOME/.ecspros/dp-keys"
DST="$HOME/yedekler/dp-keys-$(date +%Y%m%d)"
mkdir -p "$DST"
cp -a "$SRC/." "$DST/"
touch "$DST"   # cp -a kaynak mtime'ını korur — temizlik taze yedeği "eski" sanmasın
# 30 günden eski dp-keys yedeklerini temizle
find "$HOME/yedekler" -maxdepth 1 -type d -name 'dp-keys-*' -mtime +30 -exec rm -rf {} \;
echo "DP key yedeği: $DST ($(ls "$DST" | wc -l) dosya)"
