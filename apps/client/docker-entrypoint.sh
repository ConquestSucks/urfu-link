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

HTML=/usr/share/nginx/html/index.html

cat > /usr/share/nginx/html/app-config.js <<EOF
window.__APP_CONFIG__ = {
    appEnv: "${APP_ENV}",
    apiUrl: "${API_URL}",
    keycloakUrl: "${KEYCLOAK_URL}"
};
EOF

# Inject app-config.js before the bundle so window.__APP_CONFIG__ is set
# before config.ts module runs. Only inject if not already present.
if ! grep -q 'app-config.js' "${HTML}"; then
    sed -i 's|<script src="/_expo/static/js/|<script src="/app-config.js"></script><script src="/_expo/static/js/|' "${HTML}"
fi

exec nginx -g 'daemon off;'
