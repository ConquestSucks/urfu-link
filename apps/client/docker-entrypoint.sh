#!/bin/sh
set -eu

APP_ENV="${APP_ENV:-dev}"

case "${APP_ENV}" in
  dev|prod) ;;
  *)
    echo "Invalid APP_ENV: ${APP_ENV}. Must be dev or prod."
    exit 1
    ;;
esac

API_URL="${EXPO_PUBLIC_API_URL:-}"
KEYCLOAK_URL="${EXPO_PUBLIC_KEYCLOAK_URL:-}"

cat > /usr/share/nginx/html/app-config.js <<EOF
window.__APP_CONFIG__ = {
    appEnv: "${APP_ENV}",
    apiUrl: "${API_URL}",
    keycloakUrl: "${KEYCLOAK_URL}"
};
EOF

exec nginx -g 'daemon off;'
