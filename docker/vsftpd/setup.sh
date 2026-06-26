#!/bin/bash
# vsftpd + media web sunucu kurulum scripti
# Çalıştır: sudo bash /opt/ECSProsAI/docker/vsftpd/setup.sh

set -e

MEDIA_DIR="/opt/ECSProsAI/media"
FTP_USER="mediauser"
FTP_PASS="EcsProsMedia2025!"   # <-- İSTEDİĞİNİZLE DEĞİŞTİRİN

echo "=== 1. vsftpd kurulumu ==="
apt-get update -qq
apt-get install -y vsftpd

echo "=== 2. FTP kullanıcısı oluşturuluyor: $FTP_USER ==="
# Kullanıcı yoksa oluştur
if ! id "$FTP_USER" &>/dev/null; then
    useradd -m -d "$MEDIA_DIR" -s /usr/sbin/nologin "$FTP_USER"
    echo "$FTP_USER:$FTP_PASS" | chpasswd
    echo "Kullanıcı oluşturuldu."
else
    echo "Kullanıcı zaten var, şifre güncelleniyor."
    echo "$FTP_USER:$FTP_PASS" | chpasswd
fi

echo "=== 3. Dizin izinleri ==="
chown -R "$FTP_USER":www-data "$MEDIA_DIR"
chmod -R 775 "$MEDIA_DIR"

echo "=== 4. vsftpd.conf kopyalanıyor ==="
cp /etc/vsftpd.conf /etc/vsftpd.conf.bak.$(date +%Y%m%d)
cp /opt/ECSProsAI/docker/vsftpd/vsftpd.conf /etc/vsftpd.conf

echo "=== 5. Kullanıcı izin listesi ==="
echo "$FTP_USER" > /etc/vsftpd.userlist

echo "=== 6. vsftpd servis başlatılıyor ==="
systemctl enable vsftpd
systemctl restart vsftpd
systemctl status vsftpd --no-pager

echo "=== 7. Firewall port açılıyor (ufw) ==="
if command -v ufw &>/dev/null; then
    ufw allow 21/tcp comment "FTP"
    ufw allow 40000:40100/tcp comment "FTP Passive"
    echo "UFW kuralları eklendi."
else
    echo "ufw bulunamadı — iptables ile manuel ekleyin:"
    echo "  iptables -A INPUT -p tcp --dport 21 -j ACCEPT"
    echo "  iptables -A INPUT -p tcp --dport 40000:40100 -j ACCEPT"
fi

echo ""
echo "========================================"
echo " KURULUM TAMAMLANDI"
echo "========================================"
echo " FTP Host : 51.178.208.59"
echo " FTP Port : 21"
echo " Kullanıcı: $FTP_USER"
echo " Şifre    : $FTP_PASS"
echo " Mod      : Passive (40000-40100)"
echo ""
echo " Dosya yolları:"
echo "   Resimler : $MEDIA_DIR/images/"
echo "   Videolar : $MEDIA_DIR/videos/"
echo ""
echo " Web URL'leri:"
echo "   http://51.178.208.59/media/images/dosya.jpg"
echo "   http://51.178.208.59/media/videos/dosya.mp4"
echo "========================================"
