// Mock Keycloak server for local dev / Claude Code preview.
// Implements just the endpoints the app needs:
//   GET  /realms/:realm/protocol/openid-connect/auth   → instant 302 with code
//   POST /realms/:realm/protocol/openid-connect/token  → fake JWT tokens
// No login form — every auth request is auto-approved as dev@urfu.ru.

'use strict';

const http = require('http');

const PORT  = parseInt(process.env.MOCK_KEYCLOAK_PORT ?? '8081', 10);
const REALM = 'urfu-link';

function base64url(obj) {
    return Buffer.from(JSON.stringify(obj)).toString('base64url');
}

function makeJwt(payload) {
    const header = base64url({ alg: 'RS256', typ: 'JWT' });
    const body   = base64url(payload);
    return `${header}.${body}.mock-signature`;
}

function makeTokens() {
    const now = Math.floor(Date.now() / 1000);
    return {
        access_token:       makeJwt({ sub: 'dev-user-id', preferred_username: 'dev', email: 'dev@urfu.ru', name: 'Dev User', iat: now, exp: now + 3600 }),
        refresh_token:      makeJwt({ sub: 'dev-user-id', typ: 'Refresh', iat: now, exp: now + 86400 }),
        token_type:         'Bearer',
        expires_in:         3600,
        refresh_expires_in: 86400,
        session_state:      'mock-session',
    };
}

function setCors(res) {
    res.setHeader('Access-Control-Allow-Origin',  '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
}

const server = http.createServer((req, res) => {
    const url = new URL(req.url, `http://localhost:${PORT}`);
    setCors(res);

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    // ── Authorization endpoint ──────────────────────────────────────────────
    if (url.pathname === `/realms/${REALM}/protocol/openid-connect/auth`) {
        const redirectUri = url.searchParams.get('redirect_uri');
        const state       = url.searchParams.get('state');

        if (!redirectUri) {
            res.writeHead(400);
            res.end('Missing redirect_uri');
            return;
        }

        const code     = `mock-code-${Date.now()}`;
        const callback = new URL(redirectUri);
        callback.searchParams.set('code', code);
        if (state) callback.searchParams.set('state', state);

        console.log(`[mock-keycloak] auth → 302 ${callback}`);
        res.writeHead(302, { Location: callback.toString() });
        res.end();
        return;
    }

    // ── Token endpoint ──────────────────────────────────────────────────────
    if (
        url.pathname === `/realms/${REALM}/protocol/openid-connect/token` &&
        req.method === 'POST'
    ) {
        const tokens = makeTokens();
        console.log('[mock-keycloak] token → OK');
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify(tokens));
        return;
    }

    // ── Fallback ────────────────────────────────────────────────────────────
    console.log(`[mock-keycloak] 404 ${req.method} ${url.pathname}`);
    res.writeHead(404);
    res.end(`Not found: ${url.pathname}`);
});

server.listen(PORT, () => {
    console.log(`[mock-keycloak] http://localhost:${PORT}/realms/${REALM}`);
});
