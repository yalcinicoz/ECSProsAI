#!/bin/bash
openssl req -x509 -nodes -days 3650 -newkey rsa:2048 -keyout /opt/ECSProsAI/docker/nginx/certs/server.key -out /opt/ECSProsAI/docker/nginx/certs/server.crt -subj "/CN=51.178.208.59"
echo "Done: $?"
ls -la /opt/ECSProsAI/docker/nginx/certs/
