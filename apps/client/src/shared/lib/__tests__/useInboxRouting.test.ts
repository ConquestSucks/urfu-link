import { renderHook } from "@testing-library/react-native";

import { useInboxRouting } from "../useInboxRouting";

let mockPathname = "/chats";
let mockSegments = ["(authorized)", "chats"];
let mockLocalParams: Record<string, string | undefined> = {};
let mockGlobalParams: Record<string, string | undefined> = {};

jest.mock("expo-router", () => ({
    usePathname: () => mockPathname,
    useSegments: () => mockSegments,
    useLocalSearchParams: () => mockLocalParams,
    useGlobalSearchParams: () => mockGlobalParams,
}));

describe("useInboxRouting", () => {
    beforeEach(() => {
        mockPathname = "/chats";
        mockSegments = ["(authorized)", "chats"];
        mockLocalParams = {};
        mockGlobalParams = {};
    });

    it("ignores stale detail params after returning to the inbox root", () => {
        mockLocalParams = { view: "messages" };
        mockGlobalParams = { id: "chat-1", view: "messages" };

        const { result } = renderHook(() => useInboxRouting());

        expect(result.current.params.id).toBeUndefined();
        expect(result.current.createViewHref("notifications")).toEqual({
            pathname: "/chats",
            params: { view: "notifications" },
        });
    });

    it("uses route segments to detect the subjects tab when pathname is stale", () => {
        mockPathname = "/chats";
        mockSegments = ["(authorized)", "subjects", "[id]"];
        mockLocalParams = { id: "subject-chat-1" };

        const { result } = renderHook(() => useInboxRouting());

        expect(result.current.currentTab).toBe("subjects");
        expect(result.current.createTabHref("chats")).toEqual({
            pathname: "/chats",
            params: { view: "messages" },
        });
    });

    it("reads the active inbox view from global route params without adopting stale ids", () => {
        mockPathname = "/chats";
        mockSegments = ["(authorized)", "chats"];
        mockLocalParams = {};
        mockGlobalParams = { id: "chat-1", view: "notifications" };

        const { result } = renderHook(() => useInboxRouting());

        expect(result.current.currentView).toBe("notifications");
        expect(result.current.params.id).toBeUndefined();
    });

    it("builds view hrefs from the inbox root instead of preserving a detail route", () => {
        mockPathname = "/chats/chat-1";
        mockSegments = ["(authorized)", "chats", "[id]"];
        mockLocalParams = { id: "chat-1", view: "messages" };

        const { result } = renderHook(() => useInboxRouting());

        expect(result.current.createViewHref("notifications")).toEqual({
            pathname: "/chats",
            params: { view: "notifications" },
        });
    });
});
