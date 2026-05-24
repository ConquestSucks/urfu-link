// Thin launcher for Claude Preview / local web dev. Just starts Expo web on :3000
// with the real Keycloak from docker-compose (urfu-keycloak on :8080) and the real
// API gateway on :5080. No mock auth server — backend services validate signatures
// against the Keycloak JWKS, so a forged JWT was never going to clear the gate.

'use strict';

const { spawn } = require('child_process');
const path = require('path');

const ROOT = path.join(__dirname, '..');

const expoEnv = {
    ...process.env,
    EXPO_PUBLIC_API_URL:      process.env.EXPO_PUBLIC_API_URL      ?? 'http://localhost:5080',
    EXPO_PUBLIC_KEYCLOAK_URL: process.env.EXPO_PUBLIC_KEYCLOAK_URL ?? 'http://localhost:8080',
};

const expo = spawn(
    'pnpm',
    ['exec', 'expo', 'start', '--web', '--port', '3000'],
    { stdio: 'inherit', env: expoEnv, cwd: ROOT, shell: true },
);

expo.on('error', (err) => console.error('[dev-web] expo error:', err));

function shutdown() {
    expo.kill();
    process.exit(0);
}

process.on('SIGINT',  shutdown);
process.on('SIGTERM', shutdown);
process.on('exit',    () => { expo.kill(); });
