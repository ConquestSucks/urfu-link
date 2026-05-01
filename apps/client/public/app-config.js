// Empty seed. In prod docker-entrypoint.sh overwrites this file with values
// from container env at startup; in dev `app.config.ts` provides apiUrl/keycloakUrl
// via Constants.expoConfig.extra and this file stays empty so it does not shadow them.
window.__APP_CONFIG__ = {};
