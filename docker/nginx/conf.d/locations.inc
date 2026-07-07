    # Media — statik dosyalar (resim ve video)
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

    # Admin Examples
    location /admin/examples/ {
        alias /usr/share/nginx/html/admin/examples/;
        try_files $uri =404;
    }

    # Admin Panel (React SPA)
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

    # Eski /store/ URL'leri için geriye dönük uyumluluk
    location ^~ /store {
        return 301 /;
    }

    # API - Backend
    client_max_body_size 50m;

    location /api/ {
        proxy_pass http://host.docker.internal:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # SignalR WebSocket
    location /hubs/ {
        proxy_pass http://host.docker.internal:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_cache_bypass $http_upgrade;
    }

    # Swagger + diğer backend endpoint'leri
    location /swagger {
        proxy_pass http://host.docker.internal:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Razor storefront — tüm bilinmeyen path'ler API host'una gider (plan B14 geçişi,
    # 2026-07-07 kullanıcı kararı: new.ecspros.com artık Razor'ı sunar).
    # Statik asset'ler (css/js/ikons/images/video/fontawesome) app'in UseStaticFiles'ından gelir.
    # Eski SPA yedeği 8080 portunda (default.conf) — SPA emekli olunca o blok kaldırılacak.
    location / {
        proxy_pass http://host.docker.internal:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
