import {
    createKeycloakLogoutUrl,
    createPomeriumSignOutUrl,
} from "../logout";

jest.mock("@/shared/lib/config", () => ({
    appConfig: {
        appEnv: "dev",
        apiUrl: "http://localhost:5080",
        keycloakUrl: "http://localhost:8080",
    },
}));

jest.mock("@/shared/store/auth-store", () => ({
    useAuthStore: {
        getState: () => ({
            clearTokens: jest.fn(),
            idToken: null,
        }),
    },
}));

describe("logout urls", () => {
    it("builds a same-origin Pomerium sign-out url with the current page as return target", () => {
        const currentHref = "https://urfu-link.ghjc.ru/profile/devices?from=settings";
        const url = new URL(createPomeriumSignOutUrl(currentHref));

        expect(url.origin).toBe("https://urfu-link.ghjc.ru");
        expect(url.pathname).toBe("/.pomerium/sign_out");
        expect(url.searchParams.get("pomerium_redirect_uri")).toBe(currentHref);
    });

    it("builds a Keycloak end-session url for the dev PKCE flow", () => {
        const url = new URL(
            createKeycloakLogoutUrl({
                keycloakUrl: "http://localhost:8080/",
                redirectUri: "http://localhost:3000/",
                idToken: "id-token",
            }),
        );

        expect(url.origin).toBe("http://localhost:8080");
        expect(url.pathname).toBe("/realms/urfu-link/protocol/openid-connect/logout");
        expect(url.searchParams.get("client_id")).toBe("urfu-link-web");
        expect(url.searchParams.get("post_logout_redirect_uri")).toBe("http://localhost:3000/");
        expect(url.searchParams.get("id_token_hint")).toBe("id-token");
    });
});
