FROM node:20-bookworm-slim AS build

WORKDIR /app

ENV NPM_CONFIG_REGISTRY=https://registry.npmjs.org/
ENV NPM_CONFIG_AUDIT=false
ENV NPM_CONFIG_FUND=false
ENV NPM_CONFIG_UPDATE_NOTIFIER=false

COPY package.json package-lock.json ./

RUN node --version \
    && npm --version \
    && npm ci --include=dev --no-audit --no-fund \
    && test -x node_modules/.bin/ng

COPY . .

RUN npm run build

FROM nginx:1.27-alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80