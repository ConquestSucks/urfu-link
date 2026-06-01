const mockBack = jest.fn();
const mockReplace = jest.fn();
const mockCanGoBack = jest.fn();

jest.mock("expo-router", () => ({
    router: {
        back: (...args: unknown[]) => mockBack(...args),
        replace: (...args: unknown[]) => mockReplace(...args),
        canGoBack: () => mockCanGoBack(),
    },
}));

const setPlatformOS = (os: string) => {
    const { Platform } = require("react-native");
    Object.defineProperty(Platform, "OS", {
        configurable: true,
        get: () => os,
    });
};

describe("safeGoBack", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        setPlatformOS("android");
        mockCanGoBack.mockReturnValue(true);
    });

    it("uses native back by default when a native back stack exists", () => {
        const { safeGoBack } = require("../safeGoBack") as typeof import("../safeGoBack");

        safeGoBack("/chats");

        expect(mockBack).toHaveBeenCalledTimes(1);
        expect(mockReplace).not.toHaveBeenCalled();
    });

    it("uses fallback replacement on native when deterministic fallback is requested", () => {
        const { safeGoBack } = require("../safeGoBack") as typeof import("../safeGoBack");

        safeGoBack("/chats", { preferFallback: true });

        expect(mockReplace).toHaveBeenCalledWith("/chats");
        expect(mockBack).not.toHaveBeenCalled();
    });

    it("uses fallback replacement when the native back stack is empty", () => {
        mockCanGoBack.mockReturnValue(false);
        const { safeGoBack } = require("../safeGoBack") as typeof import("../safeGoBack");

        safeGoBack("/chats");

        expect(mockReplace).toHaveBeenCalledWith("/chats");
        expect(mockBack).not.toHaveBeenCalled();
    });

    it("always uses fallback replacement on web", () => {
        setPlatformOS("web");
        const { safeGoBack } = require("../safeGoBack") as typeof import("../safeGoBack");

        safeGoBack("/chats");

        expect(mockReplace).toHaveBeenCalledWith("/chats");
        expect(mockBack).not.toHaveBeenCalled();
    });
});
