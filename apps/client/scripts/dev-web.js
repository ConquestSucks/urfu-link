// Starts mock-keycloak + Expo web dev server together.
// Used by .claude/launch.json so the preview works without Docker.

'use strict';

const { fork, spawn } = require('child_process');
const path = require('path');

const ROOT = path.join(__dirname, '..');

// ── 1. Mock Keycloak ────────────────────────────────────────────────────────
const keycloak = fork(path.join(__dirname, 'mock-keycloak.js'), [], {
    stdio: 'inherit',
    env: { ...process.env, MOCK_KEYCLOAK_PORT: '8081' },
});

keycloak.on('error', (err) => console.error('[dev-web] mock-keycloak error:', err));

// ── 2. Expo web ─────────────────────────────────────────────────────────────
const expoEnv = {
    ...process.env,
    EXPO_PUBLIC_API_URL:      process.env.EXPO_PUBLIC_API_URL      ?? 'http://localhost:5080',
    EXPO_PUBLIC_KEYCLOAK_URL: process.env.EXPO_PUBLIC_KEYCLOAK_URL ?? 'http://localhost:8081',
};

const expo = spawn(
    'pnpm',
    ['exec', 'expo', 'start', '--web', '--port', '3000'],
    { stdio: 'inherit', env: expoEnv, cwd: ROOT, shell: true },
);

expo.on('error', (err) => console.error('[dev-web] expo error:', err));

// ── Cleanup ─────────────────────────────────────────────────────────────────
function shutdown() {
    keycloak.kill();
    expo.kill();
    process.exit(0);
}

process.on('SIGINT',  shutdown);
process.on('SIGTERM', shutdown);
process.on('exit',    () => { keycloak.kill(); expo.kill(); });
