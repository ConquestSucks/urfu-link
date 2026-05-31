import { getNotificationScopeUnreadCount } from "../scoped-badge";

describe("getNotificationScopeUnreadCount", () => {
    it("uses backend scoped counts when present", () => {
        expect(
            getNotificationScopeUnreadCount(
                {
                    total: 10,
                    perCategory: { 1: 5, 3: 5 },
                    totalUnseen: 10,
                    urgentUnread: 0,
                    perType: null,
                    scopes: { chats: 2, disciplines: 8 },
                },
                "disciplines",
            ),
        ).toBe(8);
    });

    it("does not show discipline badge for direct-only fallback categories", () => {
        expect(
            getNotificationScopeUnreadCount(
                {
                    total: 1,
                    perCategory: { 1: 1 },
                    totalUnseen: 1,
                    urgentUnread: 0,
                    perType: null,
                },
                "disciplines",
            ),
        ).toBe(0);
    });

    it("does not guess ambiguous pinned notifications as direct chats without backend scopes", () => {
        const badge = {
            total: 1,
            perCategory: { 7: 1 },
            totalUnseen: 1,
            urgentUnread: 0,
            perType: null,
        };

        expect(getNotificationScopeUnreadCount(badge, "chats")).toBe(0);
        expect(getNotificationScopeUnreadCount(badge, "disciplines")).toBe(0);
    });
});
