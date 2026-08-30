# Telemania demo — admin panel location'ları. Backend hedefi demo API (:5050).
# Kök /admin/'e yönlendirilir; /onizleme ürün kartı SSR önizlemesi içindir.
# NOT: Fulfillment etiket yazdırma iframe'leri (Razor /yazdir/* gibi) bu hostta kök
# redirect'e takılır — demo kapsamında değildir; gerekirse ilgili path'ler :5050'ye eklenmeli.

    # Media — ürün görselleri (üretimle ortak media/ dizini)
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

    # Admin SPA (base=/admin)
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

    # Ürün kartı SSR önizleme iframe'i → demo Razor
    location /onizleme {
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $gercek_istemci_ip;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # API — demo backend
    client_max_body_size 50m;

    location /api/ {
        limit_req zone=ecs_api burst=100 nodelay;
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

    # Swagger (demo API dokümanı)
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

    # Kök → admin paneline yönlendir
    location / {
        return 301 /admin/;
    }
