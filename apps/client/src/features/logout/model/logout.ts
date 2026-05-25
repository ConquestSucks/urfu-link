import { KEYCLOAK_CLIENT_ID, KEYCLOAK_REALM } from "@/features/auth/keycloak-constants";
import { appConfig } from "@/shared/lib/config";
import { useAuthStore } from "@/shared/store/auth-store";
import { Platform } from "react-native";

type KeycloakLogoutUrlParams = {
    keycloakUrl: string;
    redirectUri: string;
    idToken?: string | null;
};

const trimTrailingSlash = (value: string) => value.replace(/\/$/, "");

export function createPomeriumSignOutUrl(currentHref: string): string {
    const url = new URL("/.pomerium/sign_out", currentHref);
    url.searchParams.set("pomerium_redirect_uri", currentHref);
    return url.toString();
}

export function createKeycloakLogoutUrl({
    keycloakUrl,
    redirectUri,
    idToken,
}: KeycloakLogoutUrlParams): string {
    const url = new URL(
        `${trimTrailingSlash(keycloakUrl)}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/logout`,
    );
    url.searchParams.set("client_id", KEYCLOAK_CLIENT_ID);
    url.searchParams.set("post_logout_redirect_uri", redirectUri);

    if (idToken) {
        url.searchParams.set("id_token_hint", idToken);
    }

    return url.toString();
}

function clearPkceState(): void {
    if (typeof window === "undefined") return;

    window.sessionStorage.removeItem("pkce_code_verifier");
    window.sessionStorage.removeItem("pkce_state");
}

export function performLogout(): void {
    const { clearTokens, idToken } = useAuthStore.getState();

    clearTokens();
    clearPkceState();

    if (Platform.OS !== "web" || typeof window === "undefined") return;

    if (appConfig.appEnv === "dev") {
        window.location.assign(
            createKeycloakLogoutUrl({
                keycloakUrl: appConfig.keycloakUrl,
                redirectUri: `${window.location.origin}/`,
                idToken,
            }),
        );
        return;
    }

    window.location.assign(createPomeriumSignOutUrl(window.location.href));
}
