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
  apiUrl: "${EXPO_PUBLIC_API_URL:-https://api.dev.127.0.0.1.nip.io}",
  oidcAuthority: "${OIDC_AUTHORITY:-}",
  oidcClientId: "${OIDC_CLIENT_ID:-}"
};
EOF

# Inject app-config.js into index.html before the closing </head> tag
sed -i 's|</head>|<script src="/app-config.js"></script></head>|' /usr/share/nginx/html/index.html

exec nginx -g 'daemon off;'
