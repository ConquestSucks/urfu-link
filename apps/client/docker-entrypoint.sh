#!/bin/sh
set -eu

case "${APP_ENV:-dev}" in
  dev|prod) ;;
  *)
    echo "Invalid APP_ENV: ${APP_ENV:-<unset>}. Must be dev or prod."
    exit 1
    ;;
esac

cat > /usr/share/nginx/html/app-config.js <<EOF
window.__APP_CONFIG__ = {
  appEnv: "${APP_ENV:-dev}",
  apiUrl: "${EXPO_PUBLIC_API_URL:-https://api.dev.127.0.0.1.nip.io}"
};
EOF

exec nginx -g 'daemon off;'
