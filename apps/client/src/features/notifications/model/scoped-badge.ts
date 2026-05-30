import type { NotificationBadgeDto } from "@urfu-link/api-client";

export type NotificationScope = "chats" | "disciplines";

const chatFallbackCategories = [1, 10, 11, 40, 41, 50, 51, 52, 53];
const disciplineFallbackCategories = [3, 20, 21, 22, 23, 24, 25, 26];

export const getNotificationScopeUnreadCount = (
    badge: NotificationBadgeDto | null | undefined,
    scope: NotificationScope,
) => {
    if (!badge) return 0;
    const scoped = badge.scopes?.[scope];
    if (typeof scoped === "number") {
        return scoped;
    }

    const categories =
        scope === "disciplines" ? disciplineFallbackCategories : chatFallbackCategories;
    return categories.reduce((sum, category) => sum + (badge.perCategory?.[category] ?? 0), 0);
};
