import { renderHook } from "@testing-library/react-native";

import { useInboxRouting } from "../useInboxRouting";

let mockPathname = "/chats";
let mockLocalParams: Record<string, string | undefined> = {};
let mockGlobalParams: Record<string, string | undefined> = {};

jest.mock("expo-router", () => ({
    usePathname: () => mockPathname,
    useLocalSearchParams: () => mockLocalParams,
    useGlobalSearchParams: () => mockGlobalParams,
}));

describe("useInboxRouting", () => {
    beforeEach(() => {
        mockPathname = "/chats";
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
});
