import { render, waitFor } from "@testing-library/react-native";
import { Platform, Text } from "react-native";

const PENDING_NATIVE_AUTH_KEY = "urfu-link:dev-auth:pkce";
const mockAuthRequestConstructor = jest.fn();
const mockAuthRequestMakeAuthUrlAsync = jest.fn<Promise<string>, [unknown?]>();
const mockAuthRequestPromptAsync = jest.fn<Promise<unknown>, [unknown?, unknown?]>();
const mockExchangeCodeAsync = jest.fn<Promise<unknown>, [unknown?, unknown?]>();
const mockMakeRedirectUri = jest.fn<string, [unknown?]>(() => "urfulink://auth");
const mockOpenBrowserAsync = jest.fn<Promise<unknown>, [string]>();
const mockRouterReplace = jest.fn();
const mockSetTokens = jest.fn();
const mockIsTokenValid = jest.fn();
const mockStorage = new Map<string, string>();
let mockLocalSearchParams: Record<string, string | string[] | undefined> = {};

class MockAuthRequest {
    codeVerifier = "verifier";
    state = "state";
    url: string | null = null;

    constructor(config: unknown) {
        mockAuthRequestConstructor(config);
    }

    async makeAuthUrlAsync(discovery: unknown): Promise<string> {
        const url = await mockAuthRequestMakeAuthUrlAsync(discovery);
        this.url = url;
        return url;
    }

    promptAsync(discovery: unknown, options: unknown): Promise<unknown> {
        return mockAuthRequestPromptAsync(discovery, options);
    }
}

jest.mock("expo-auth-session", () => ({
    AuthRequest: MockAuthRequest,
    ResponseType: {
        Code: "code",
    },
    exchangeCodeAsync: (config: unknown, discovery: unknown) => mockExchangeCodeAsync(config, discovery),
    makeRedirectUri: (options: unknown) => mockMakeRedirectUri(options),
}));

jest.mock("expo-web-browser", () => ({
    maybeCompleteAuthSession: jest.fn(),
    openBrowserAsync: (url: string) => mockOpenBrowserAsync(url),
}));

jest.mock("expo-router", () => ({
    useLocalSearchParams: () => mockLocalSearchParams,
    useRouter: () => ({
        replace: mockRouterReplace,
    }),
}));

jest.mock("@/shared/lib/config", () => ({
    appConfig: {
        apiUrl: "https://api.example.test",
        appEnv: "dev",
        keycloakUrl: "https://id.example.test",
    },
}));

jest.mock("@/shared/lib/storage", () => ({
    appStorage: {
        getItem: (key: string) => mockStorage.get(key) ?? null,
        removeItem: (key: string) => {
            mockStorage.delete(key);
        },
        setItem: (key: string, value: string) => {
            mockStorage.set(key, value);
        },
    },
}));

jest.mock("@/shared/store/auth-store", () => ({
    useAuthStore: () => ({
        isTokenValid: mockIsTokenValid,
        setTokens: mockSetTokens,
    }),
}));

const { AuthGate } = require("../auth-gate") as typeof import("../auth-gate");
const { AuthCallbackScreen } = require("../auth-callback") as typeof import("../auth-callback");
const originalPlatformOS = Platform.OS;

describe("AuthGate", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockStorage.clear();
        mockLocalSearchParams = {};
        Object.defineProperty(Platform, "OS", { configurable: true, value: "android" });
        mockAuthRequestMakeAuthUrlAsync.mockResolvedValue("https://id.example.test/auth");
        mockAuthRequestPromptAsync.mockResolvedValue({ type: "dismiss" });
        mockOpenBrowserAsync.mockResolvedValue({ type: "opened" });
        mockExchangeCodeAsync.mockResolvedValue({
            accessToken: "access-token",
            expiresIn: 300,
            idToken: "id-token",
            issuedAt: 1_700_000_000,
            refreshToken: "refresh-token",
        });
        mockIsTokenValid.mockReturnValue(false);
    });

    afterAll(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
    });

    it("starts native PKCE authorization and persists callback state", async () => {
        render(
            <AuthGate>
                <Text>content</Text>
            </AuthGate>,
        );

        await waitFor(() => {
            expect(mockOpenBrowserAsync).toHaveBeenCalledWith("https://id.example.test/auth");
        });

        expect(mockAuthRequestConstructor).toHaveBeenCalledWith(
            expect.objectContaining({
                clientId: "urfu-link-web",
                redirectUri: "urfulink://auth",
                responseType: "code",
                scopes: ["openid", "profile", "email", "offline_access"],
                usePKCE: true,
            }),
        );
        expect(mockAuthRequestPromptAsync).not.toHaveBeenCalled();
        expect(JSON.parse(mockStorage.get(PENDING_NATIVE_AUTH_KEY) ?? "{}")).toEqual({
            codeVerifier: "verifier",
            createdAt: expect.any(Number),
            state: "state",
        });
    });

    it("routes direct native prompt success to the callback screen", async () => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "ios" });
        mockAuthRequestPromptAsync.mockResolvedValue({
            params: {
                code: "auth-code",
                state: "state",
            },
            type: "success",
        });

        render(
            <AuthGate>
                <Text>content</Text>
            </AuthGate>,
        );

        await waitFor(() => {
            expect(mockRouterReplace).toHaveBeenCalledWith({
                params: {
                    code: "auth-code",
                    state: "state",
                },
                pathname: "/auth",
            });
        });

        expect(mockOpenBrowserAsync).not.toHaveBeenCalled();
        expect(mockExchangeCodeAsync).not.toHaveBeenCalled();
        expect(mockSetTokens).not.toHaveBeenCalled();
        expect(JSON.parse(mockStorage.get(PENDING_NATIVE_AUTH_KEY) ?? "{}")).toEqual({
            codeVerifier: "verifier",
            createdAt: expect.any(Number),
            state: "state",
        });
    });
});

describe("AuthCallbackScreen", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockStorage.clear();
        mockLocalSearchParams = {
            code: "auth-code",
            state: "state",
        };
        mockIsTokenValid.mockReturnValue(false);
        mockExchangeCodeAsync.mockResolvedValue({
            accessToken: "access-token",
            expiresIn: 300,
            idToken: "id-token",
            issuedAt: 1_700_000_000,
            refreshToken: "refresh-token",
        });
        mockStorage.set(
            PENDING_NATIVE_AUTH_KEY,
            JSON.stringify({
                codeVerifier: "verifier",
                createdAt: 1_700_000_000_000,
                state: "state",
            }),
        );
    });

    it("exchanges native callback params and leaves the callback route", async () => {
        render(<AuthCallbackScreen />);

        await waitFor(() => {
            expect(mockSetTokens).toHaveBeenCalledWith(
                "access-token",
                "refresh-token",
                1_700_000_300_000,
                "id-token",
            );
        });

        expect(mockRouterReplace).toHaveBeenCalledWith({
            params: {
                view: "messages",
            },
            pathname: "/chats",
        });
        expect(mockStorage.has(PENDING_NATIVE_AUTH_KEY)).toBe(false);
    });
});
