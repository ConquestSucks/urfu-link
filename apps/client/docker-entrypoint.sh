#!/bin/sh
set -eu

case "${APP_ENV:-dev}" in
  dev|prod) ;;
  *)
    echo "Invalid APP_ENV: ${APP_ENV:-<unset>}. Must be dev or prod."
    exit 1
    ;;
esac

exec nginx -g 'daemon off;'
