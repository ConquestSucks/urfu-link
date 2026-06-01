import { appConfig } from "@/shared/lib/config";
import { appStorage } from "@/shared/lib/storage";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import { Platform } from "react-native";
import { KEYCLOAK_CLIENT_ID, KEYCLOAK_REALM } from "../keycloak-constants";

const SCOPES = ["openid", "profile", "email", "offline_access"] as const;
const WEB_SCOPES = SCOPES.join(" ");
const NATIVE_REDIRECT_OPTIONS = {
    path: "auth",
    scheme: "urfulink",
} as const;
export const PENDING_NATIVE_AUTH_KEY = "urfu-link:dev-auth:pkce";

type PendingNativeAuth = {
    codeVerifier: string;
    createdAt: number;
    state: string;
};

export type AuthTokens = {
    accessToken: string;
    expiresAt: number;
    idToken: string | null;
    refreshToken: string;
};

export type NativeAuthorizationStartResult =
    | AuthSession.AuthSessionResult
    | {
        type: "opened";
    };

const trimTrailingSlash = (value: string) => value.replace(/\/$/, "");

export function createKeycloakDiscovery() {
    const realmUrl = `${trimTrailingSlash(appConfig.keycloakUrl)}/realms/${KEYCLOAK_REALM}/protocol/openid-connect`;

    return {
        authorizationEndpoint: `${realmUrl}/auth`,
        revocationEndpoint: `${realmUrl}/revoke`,
        tokenEndpoint: `${realmUrl}/token`,
    };
}

export function getNativeRedirectUri(): string {
    return AuthSession.makeRedirectUri(NATIVE_REDIRECT_OPTIONS);
}

function getWebRedirectUri(): string {
    return `${window.location.origin}/`;
}

function base64urlEncode(bytes: Uint8Array): string {
    return btoa(String.fromCharCode(...bytes))
        .replace(/\+/g, "-")
        .replace(/\//g, "_")
        .replace(/=/g, "");
}

async function generateCodeVerifier(): Promise<string> {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return base64urlEncode(array);
}

async function generateCodeChallenge(verifier: string): Promise<string> {
    const data = new TextEncoder().encode(verifier);
    const digest = await crypto.subtle.digest("SHA-256", data);
    return base64urlEncode(new Uint8Array(digest));
}

function getTokenExpiresAt(token: AuthSession.TokenResponse): number {
    if (typeof token.expiresIn !== "number") {
        return Date.now() + 3_600_000;
    }

    const issuedAtMs = typeof token.issuedAt === "number" ? token.issuedAt * 1000 : Date.now();
    return issuedAtMs + token.expiresIn * 1000;
}

function tokenResponseToAuthTokens(token: AuthSession.TokenResponse): AuthTokens {
    return {
        accessToken: token.accessToken,
        expiresAt: getTokenExpiresAt(token),
        idToken: token.idToken ?? null,
        refreshToken: token.refreshToken ?? "",
    };
}

function savePendingNativeAuth(request: AuthSession.AuthRequest): void {
    if (!request.codeVerifier) {
        throw new Error("PKCE code verifier was not generated.");
    }

    const pendingAuth: PendingNativeAuth = {
        codeVerifier: request.codeVerifier,
        createdAt: Date.now(),
        state: request.state,
    };

    appStorage.setItem(PENDING_NATIVE_AUTH_KEY, JSON.stringify(pendingAuth));
}

function readPendingNativeAuth(): PendingNativeAuth | null {
    const raw = appStorage.getItem(PENDING_NATIVE_AUTH_KEY) as string | null;
    if (!raw) {
        return null;
    }

    try {
        const parsed = JSON.parse(raw) as Partial<PendingNativeAuth>;
        if (
            typeof parsed.codeVerifier !== "string" ||
            typeof parsed.createdAt !== "number" ||
            typeof parsed.state !== "string"
        ) {
            return null;
        }

        return parsed as PendingNativeAuth;
    } catch {
        return null;
    }
}

export function clearPendingNativeAuth(): void {
    appStorage.removeItem(PENDING_NATIVE_AUTH_KEY);
}

export async function startNativePKCEFlow(): Promise<NativeAuthorizationStartResult> {
    const discovery = createKeycloakDiscovery();
    const request = new AuthSession.AuthRequest({
        clientId: KEYCLOAK_CLIENT_ID,
        redirectUri: getNativeRedirectUri(),
        responseType: AuthSession.ResponseType.Code,
        scopes: [...SCOPES],
        usePKCE: true,
    });

    const authUrl = await request.makeAuthUrlAsync(discovery);
    savePendingNativeAuth(request);

    if (Platform.OS === "android") {
        await WebBrowser.openBrowserAsync(authUrl);
        return { type: "opened" };
    }

    return request.promptAsync(discovery, { url: authUrl });
}

export async function exchangeNativeAuthorizationCode({
    code,
    state,
}: {
    code: string;
    state: string;
}): Promise<AuthTokens> {
    const pendingAuth = readPendingNativeAuth();
    if (!pendingAuth) {
        throw new Error("Missing pending authorization request.");
    }

    clearPendingNativeAuth();

    if (pendingAuth.state !== state) {
        throw new Error("Authorization state does not match.");
    }

    const token = await AuthSession.exchangeCodeAsync(
        {
            clientId: KEYCLOAK_CLIENT_ID,
            code,
            extraParams: {
                code_verifier: pendingAuth.codeVerifier,
            },
            redirectUri: getNativeRedirectUri(),
        },
        createKeycloakDiscovery(),
    );

    return tokenResponseToAuthTokens(token);
}

export async function startWebPKCEFlow(): Promise<void> {
    const redirectUri = getWebRedirectUri();
    const codeVerifier = await generateCodeVerifier();
    const codeChallenge = await generateCodeChallenge(codeVerifier);
    const state = base64urlEncode(crypto.getRandomValues(new Uint8Array(16)));

    sessionStorage.setItem("pkce_code_verifier", codeVerifier);
    sessionStorage.setItem("pkce_state", state);

    const params = new URLSearchParams({
        client_id: KEYCLOAK_CLIENT_ID,
        code_challenge: codeChallenge,
        code_challenge_method: "S256",
        redirect_uri: redirectUri,
        response_type: "code",
        scope: WEB_SCOPES,
        state,
    });

    window.location.href = `${appConfig.keycloakUrl}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/auth?${params}`;
}

export async function handleWebCallback(
    code: string,
    state: string,
): Promise<AuthTokens | null> {
    const redirectUri = getWebRedirectUri();
    const storedVerifier = sessionStorage.getItem("pkce_code_verifier");
    const storedState = sessionStorage.getItem("pkce_state");

    if (!storedVerifier || storedState !== state) return null;

    sessionStorage.removeItem("pkce_code_verifier");
    sessionStorage.removeItem("pkce_state");

    const res = await fetch(
        `${appConfig.keycloakUrl}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`,
        {
            body: new URLSearchParams({
                client_id: KEYCLOAK_CLIENT_ID,
                code,
                code_verifier: storedVerifier,
                grant_type: "authorization_code",
                redirect_uri: redirectUri,
            }).toString(),
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            method: "POST",
        },
    );

    if (!res.ok) return null;
    const data = await res.json();
    return {
        accessToken: data.access_token,
        expiresAt: Date.now() + data.expires_in * 1000,
        idToken: typeof data.id_token === "string" ? data.id_token : null,
        refreshToken: data.refresh_token ?? "",
    };
}
