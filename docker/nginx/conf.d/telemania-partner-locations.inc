# Telemania demo — satıcı paneli location'ları. Backend hedefi demo API (:5050).
# Üretim satici-locations.inc ile aynı yapı; yalnız upstream :5050'dir.

    # API — demo backend
    location /api/supplier/auth/ {
        limit_req zone=ecs_auth burst=20 nodelay;
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    location /api/ {
        limit_req zone=ecs_api burst=100 nodelay;
        proxy_pass http://host.docker.internal:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # SPA — kökten
    location / {
        root /usr/share/nginx/html/satici;
        index index.html;
        try_files $uri $uri/ /index.html;

        location ~* \.html$ {
            root /usr/share/nginx/html/satici;
            add_header Cache-Control "no-cache, no-store, must-revalidate";
            expires 0;
        }
        location ~* \.(js|css|svg|png|ico|woff2?)$ {
            root /usr/share/nginx/html/satici;
            add_header Cache-Control "public, max-age=604800";
            expires 7d;
        }
    }
