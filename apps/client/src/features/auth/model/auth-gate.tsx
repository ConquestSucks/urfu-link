import { appConfig } from "@/shared/lib/config";
import { useAuthStore } from "@/shared/store/auth-store";
import { useEffect, useState } from "react";
import type { PropsWithChildren } from "react";
import { ActivityIndicator, View } from "react-native";

const CLIENT_ID = "urfu-link-web";
const SCOPES = "openid profile email offline_access";
const REDIRECT_URI = "http://localhost:3000/";

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

async function startPKCEFlow(): Promise<void> {
    const codeVerifier = await generateCodeVerifier();
    const codeChallenge = await generateCodeChallenge(codeVerifier);
    const state = base64urlEncode(crypto.getRandomValues(new Uint8Array(16)));

    sessionStorage.setItem("pkce_code_verifier", codeVerifier);
    sessionStorage.setItem("pkce_state", state);

    const params = new URLSearchParams({
        response_type: "code",
        client_id: CLIENT_ID,
        redirect_uri: REDIRECT_URI,
        scope: SCOPES,
        state,
        code_challenge: codeChallenge,
        code_challenge_method: "S256",
    });

    window.location.href = `${appConfig.keycloakUrl}/realms/urfu-link/protocol/openid-connect/auth?${params}`;
}

async function handleCallback(
    code: string,
    state: string
): Promise<{ accessToken: string; refreshToken: string; expiresAt: number } | null> {
    const storedVerifier = sessionStorage.getItem("pkce_code_verifier");
    const storedState = sessionStorage.getItem("pkce_state");

    if (!storedVerifier || storedState !== state) return null;

    sessionStorage.removeItem("pkce_code_verifier");
    sessionStorage.removeItem("pkce_state");

    const res = await fetch(
        `${appConfig.keycloakUrl}/realms/urfu-link/protocol/openid-connect/token`,
        {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams({
                grant_type: "authorization_code",
                client_id: CLIENT_ID,
                code,
                redirect_uri: REDIRECT_URI,
                code_verifier: storedVerifier,
            }).toString(),
        }
    );

    if (!res.ok) return null;
    const data = await res.json();
    return {
        accessToken: data.access_token,
        refreshToken: data.refresh_token ?? "",
        expiresAt: Date.now() + data.expires_in * 1000,
    };
}

function DevAuthGate({ children }: PropsWithChildren) {
    const { isTokenValid, setTokens } = useAuthStore();
    const [exchanging, setExchanging] = useState(false);

    useEffect(() => {
        const url = new URL(window.location.href);
        const code = url.searchParams.get("code");
        const state = url.searchParams.get("state");

        if (code && state) {
            // Remove OAuth params from URL before doing anything else
            window.history.replaceState({}, "", window.location.pathname);
            setExchanging(true);
            handleCallback(code, state)
                .then((result) => {
                    if (result) {
                        setTokens(result.accessToken, result.refreshToken, result.expiresAt);
                    }
                })
                .finally(() => setExchanging(false));
            return;
        }

        if (!isTokenValid()) {
            startPKCEFlow();
        }
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    if (exchanging || !isTokenValid()) {
        return (
            <View className="flex-1 items-center justify-center bg-app-bg">
                <ActivityIndicator size="large" />
            </View>
        );
    }

    return <>{children}</>;
}

export function AuthGate({ children }: PropsWithChildren) {
    // In production Pomerium handles auth before the request reaches the app
    if (appConfig.appEnv !== "dev") {
        return <>{children}</>;
    }

    return <DevAuthGate>{children}</DevAuthGate>;
}
