import type { MessageDto, NotificationDto, UserNotifications } from "@urfu-link/api-client";
import { inferNotificationScope, resolveNotificationDeepLink } from "./notificationDeepLink";

type BrowserNotificationInput = {
    key: string;
    dedupeKeys?: string[];
    title: string;
    body: string;
    href?: string | null;
    conversationId?: string | null;
};

const DEFAULT_PREFERENCES: Pick<UserNotifications, "mutedConversationIds"> = {
    mutedConversationIds: [],
};

let preferences = DEFAULT_PREFERENCES;
const shownKeys = new Set<string>();
let navigateToHref = (href: string) => window.location.assign(href);

const isSupported = () =>
    typeof window !== "undefined" &&
    typeof document !== "undefined" &&
    "Notification" in window;

const isPageActive = () =>
    typeof document !== "undefined" &&
    document.visibilityState === "visible" &&
    (typeof document.hasFocus !== "function" || document.hasFocus());

const isMuted = (conversationId?: string | null) =>
    !!conversationId && preferences.mutedConversationIds.includes(conversationId);

const getNotificationCtor = () =>
    isSupported() ? (window as unknown as { Notification: typeof Notification }).Notification : null;

const buildConversationHref = (
    conversationId: string,
    messageId: string | null | undefined,
    isDiscipline: boolean,
) => {
    const tab = isDiscipline || conversationId.startsWith("discipline:") ? "subjects" : "chats";
    const path = `/${tab}/${encodeURIComponent(conversationId)}`;
    return messageId ? `${path}?message=${encodeURIComponent(messageId)}` : path;
};

const buildConversationMessageKey = (
    conversationId?: string | null,
    messageId?: string | null,
) => conversationId && messageId ? `message:${conversationId}:${messageId}` : null;

const showBrowserNotification = (input: BrowserNotificationInput): boolean => {
    const NotificationCtor = getNotificationCtor();
    if (!NotificationCtor || NotificationCtor.permission !== "granted") return false;
    if (isPageActive()) return false;
    if (isMuted(input.conversationId)) return false;

    const dedupeKeys = [
        input.key,
        ...(input.dedupeKeys ?? []),
    ].filter((key, index, keys): key is string => !!key && keys.indexOf(key) === index);

    if (dedupeKeys.some((key) => shownKeys.has(key))) return false;
    dedupeKeys.forEach((key) => shownKeys.add(key));

    const notification = new NotificationCtor(input.title, {
        body: input.body,
        icon: "/favicon.png",
        tag: input.key,
    });

    notification.onclick = () => {
        notification.close();
        window.focus?.();
        if (input.href) {
            navigateToHref(input.href);
        }
    };

    return true;
};

export const configureBrowserNotifications = (
    nextPreferences: UserNotifications | null | undefined,
) => {
    preferences = {
        mutedConversationIds: nextPreferences?.mutedConversationIds ?? [],
    };
};

export const getBrowserNotificationPermission = (): NotificationPermission | "unsupported" => {
    const NotificationCtor = getNotificationCtor();
    return NotificationCtor?.permission ?? "unsupported";
};

export const requestBrowserNotificationPermission = async (): Promise<
    NotificationPermission | "unsupported"
> => {
    const NotificationCtor = getNotificationCtor();
    if (!NotificationCtor) return "unsupported";
    if (NotificationCtor.permission !== "default") return NotificationCtor.permission;
    return NotificationCtor.requestPermission();
};

export const showMessageBrowserNotification = (
    message: MessageDto,
    context: {
        currentUserId?: string | null;
        title?: string | null;
        isDiscipline?: boolean;
    } = {},
) => {
    if (context.currentUserId && message.senderId === context.currentUserId) return false;

    return showBrowserNotification({
        key: buildConversationMessageKey(message.conversationId, message.id) ?? `message:${message.id}`,
        title: context.title || "Новое сообщение",
        body: message.body || (message.attachments.length > 0 ? "Вложение" : "Новое сообщение"),
        href: buildConversationHref(
            message.conversationId,
            message.id,
            context.isDiscipline ?? false,
        ),
        conversationId: message.conversationId,
    });
};

export const showServerBrowserNotification = (notification: NotificationDto) => {
    const scope = inferNotificationScope(notification);
    const target = resolveNotificationDeepLink(notification.deepLink, scope);
    const conversationId = notification.data.conversationId;
    const messageId = notification.data.messageId;
    const conversationMessageKey = buildConversationMessageKey(conversationId, messageId);

    return showBrowserNotification({
        key: `notification:${notification.id}`,
        dedupeKeys: conversationMessageKey ? [conversationMessageKey] : [],
        title: notification.title,
        body: notification.body,
        href: typeof target?.href === "string" ? target.href : null,
        conversationId,
    });
};

export const resetBrowserNotificationsForTests = () => {
    preferences = DEFAULT_PREFERENCES;
    shownKeys.clear();
    navigateToHref = (href: string) => window.location.assign(href);
};

export const setBrowserNotificationNavigationForTests = (
    navigate: ((href: string) => void) | null,
) => {
    navigateToHref = navigate ?? ((href: string) => window.location.assign(href));
};
