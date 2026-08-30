# Telemania demo — ortak location'lar. Üretim locations.inc ile aynı yapı,
# yalnız backend hedefi demo API'ye (:5050) gider. Media ortak (aynı mount).

    # Media — statik dosyalar (resim ve video) — üretimle ortak media/ dizini
    location /media/ {
        alias /usr/share/nginx/html/media/;
        try_files $uri =404;

        location ~* \.(jpg|jpeg|png|gif|webp|svg|ico)$ {
            add_header Cache-Control "public, max-age=2592000";
            add_header Access-Control-Allow-Origin "*";
            expires 30d;
        }

        location ~* \.(mp4|webm|ogg|mov|avi)$ {
            add_header Cache-Control "public, max-age=2592000";
            add_header Access-Control-Allow-Origin "*";
            expires 30d;
        }
    }

    # Admin Panel Kullanıcı Rehberi — statik site
    location /rehber/ {
        alias /usr/share/nginx/html/rehber/;
        index index.html;
        try_files $uri $uri/ /rehber/index.html;
    }
    location = /rehber {
        return 301 /rehber/;
    }

    # Admin Panel (React SPA) — demo API'ye bağlı
    location /admin/ {
        alias /usr/share/nginx/html/admin/;
        index index.html;
        try_files $uri $uri/ /admin/index.html;

        location ~* \.html$ {
            add_header Cache-Control "no-cache, no-store, must-revalidate";
            add_header Pragma "no-cache";
            expires 0;
        }
        location ~* \.(js|css)$ {
            add_header Cache-Control "public, max-age=31536000, immutable";
        }
    }
    location = /admin {
        return 301 /admin/;
    }

    # API — demo backend
    client_max_body_size 50m;

    location /api/ {
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $gercek_istemci_ip;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # SignalR WebSocket
    location /hubs/ {
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_cache_bypass $http_upgrade;
    }

    # Swagger + diğer backend endpoint'leri
    location /swagger {
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $gercek_istemci_ip;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Razor storefront — demo vitrin
    location / {
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $gercek_istemci_ip;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
