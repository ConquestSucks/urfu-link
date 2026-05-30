import type { Href } from "expo-router";
import type { NotificationDto } from "@urfu-link/api-client";

type InboxScope = "chats" | "subjects";

export type NotificationNavigationTarget = {
    href: Href;
    messageId?: string;
    threadRootMessageId?: string;
    threadMessageId?: string;
};

const disciplineCategories = new Set([3, 20, 21, 22, 23, 24, 25, 26]);

const compactGuidPattern = /^[0-9a-f]{32}$/i;
const guidPattern =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type ParsedDeepLink = {
    host: string;
    segments: string[];
};

const parseDeepLink = (deepLink: string | null | undefined): ParsedDeepLink | null => {
    if (!deepLink) return null;

    const match = /^urfulink:\/\/([^/?#]+)([^?#]*)/i.exec(deepLink.trim());
    if (!match) return null;

    return {
        host: decodeURIComponent(match[1].toLowerCase()),
        segments: match[2]
            .split("/")
            .filter(Boolean)
            .map((segment) => decodeURIComponent(segment)),
    };
};

const toHyphenatedGuidIfCompact = (value: string): string => {
    if (!compactGuidPattern.test(value)) return value;

    const lower = value.toLowerCase();
    return `${lower.slice(0, 8)}-${lower.slice(8, 12)}-${lower.slice(12, 16)}-${lower.slice(16, 20)}-${lower.slice(20)}`;
};

const toCompactGuidIfGuid = (value: string): string =>
    guidPattern.test(value) ? value.replaceAll("-", "").toLowerCase() : value.toLowerCase();

const isDisciplineConversationId = (conversationId: string): boolean =>
    conversationId.toLowerCase().startsWith("discipline:");

const encodePathSegment = (value: string): string => encodeURIComponent(value);

const buildHref = (
    path: string,
    query: Record<string, string | null | undefined> = {},
): Href => {
    const entries = Object.entries(query).filter((entry): entry is [string, string] => {
        const value = entry[1];
        return typeof value === "string" && value.length > 0;
    });

    if (entries.length === 0) return path as Href;

    const queryString = entries
        .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
        .join("&");

    return `${path}?${queryString}` as Href;
};

const resolveConversationHref = (
    conversationId: string,
    fallbackScope: InboxScope,
    query: Record<string, string | null | undefined> = {},
): Href => {
    const tab =
        isDisciplineConversationId(conversationId) || fallbackScope === "subjects"
            ? "subjects"
            : "chats";

    return buildHref(`/${tab}/${encodePathSegment(conversationId)}`, query);
};

const resolveChatLink = (
    segments: string[],
    scope: InboxScope,
): NotificationNavigationTarget | null => {
    if (segments[0] !== "conv" || !segments[1]) return null;

    const conversationId = segments[1];
    const threadIndex = segments.indexOf("thread");
    const messageIndex = segments.indexOf("msg");

    if (threadIndex >= 0 && segments[threadIndex + 1]) {
        const threadRootMessageId = toHyphenatedGuidIfCompact(segments[threadIndex + 1]);
        const threadMessageId =
            messageIndex >= 0 && segments[messageIndex + 1]
                ? toHyphenatedGuidIfCompact(segments[messageIndex + 1])
                : undefined;

        return {
            href: resolveConversationHref(conversationId, scope, {
                thread: threadRootMessageId,
                message: threadMessageId,
            }),
            messageId: threadRootMessageId,
            threadRootMessageId,
            threadMessageId,
        };
    }

    const messageId =
        messageIndex >= 0 && segments[messageIndex + 1]
            ? toHyphenatedGuidIfCompact(segments[messageIndex + 1])
            : undefined;

    return {
        href: resolveConversationHref(conversationId, scope, { message: messageId }),
        messageId,
    };
};

const resolveDisciplineLink = (segments: string[]): NotificationNavigationTarget | null => {
    if (!segments[0]) return null;

    const disciplineId = toCompactGuidIfGuid(segments[0]);
    const action = segments[1];
    const target = segments[2] ? toHyphenatedGuidIfCompact(segments[2]) : undefined;
    const conversationId = `discipline:${disciplineId}`;

    return {
        href: buildHref(`/subjects/${encodePathSegment(conversationId)}`, {
            disciplineAction: action,
            target,
        }),
    };
};

const resolveMediaLink = (segments: string[]): NotificationNavigationTarget => ({
    href: buildHref("/profile/media", {
        asset: segments[0] ? toHyphenatedGuidIfCompact(segments[0]) : undefined,
    }),
});

const resolveCallLink = (segments: string[]): NotificationNavigationTarget => ({
    href: segments[0] ? `/call/${toHyphenatedGuidIfCompact(segments[0])}` as Href : "/call" as Href,
});

const resolveSystemLink = (segments: string[]): NotificationNavigationTarget => ({
    href: buildHref("/profile/notifications", {
        source: segments[0] ?? "system",
        target: segments[1] ? toHyphenatedGuidIfCompact(segments[1]) : undefined,
    }),
});

export const resolveNotificationDeepLink = (
    deepLink: string | null | undefined,
    scope: InboxScope,
): NotificationNavigationTarget | null => {
    const parsed = parseDeepLink(deepLink);
    if (!parsed) return null;

    switch (parsed.host) {
        case "chat":
            return resolveChatLink(parsed.segments, scope);
        case "discipline":
            return resolveDisciplineLink(parsed.segments);
        case "media":
            return resolveMediaLink(parsed.segments);
        case "call":
            return resolveCallLink(parsed.segments);
        case "account":
            return { href: "/profile" as Href };
        case "system":
            return resolveSystemLink(parsed.segments);
        default:
            return {
                href: buildHref(`/${scope}`, { view: "notifications" }),
            };
    }
};

export const inferNotificationScope = (
    notification: Pick<NotificationDto, "category" | "deepLink" | "type">,
): InboxScope => {
    const parsed = parseDeepLink(notification.deepLink);

    if (parsed?.host === "discipline") return "subjects";
    if (
        parsed?.host === "chat" &&
        parsed.segments[0] === "conv" &&
        parsed.segments[1] &&
        isDisciplineConversationId(parsed.segments[1])
    ) {
        return "subjects";
    }

    if (notification.type.startsWith("discipline.")) return "subjects";
    if (disciplineCategories.has(notification.category)) return "subjects";

    return "chats";
};
