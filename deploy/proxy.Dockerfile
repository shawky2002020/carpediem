# syntax=docker/dockerfile:1.7
# ADR-008: Local reverse proxy + static Angular 22 SPA container emulating CloudFront/ALB same-origin split.

FROM node:22-alpine AS frontend-build
WORKDIR /workspace/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
COPY design-system/ /workspace/design-system/
RUN npm run build

FROM nginx:1.27-alpine AS runtime
RUN apk add --no-cache openssl \
    && mkdir -p /etc/nginx/certs \
    && openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout /etc/nginx/certs/localhost.key \
        -out /etc/nginx/certs/localhost.crt \
        -subj "/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=frontend-build /workspace/frontend/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80 443
