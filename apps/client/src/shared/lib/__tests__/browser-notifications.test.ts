import {
    configureBrowserNotifications,
    getBrowserNotificationPermission,
    requestBrowserNotificationPermission,
    resetBrowserNotificationsForTests,
    setBrowserNotificationNavigationForTests,
    showMessageBrowserNotification,
    showServerBrowserNotification,
} from "../browser-notifications";
import type { NotificationDto } from "@urfu-link/api-client";

type MockNotificationInstance = {
    title: string;
    options: NotificationOptions;
    onclick: (() => void) | null;
    close: jest.Mock;
};

const instances: MockNotificationInstance[] = [];

class MockNotification {
    static permission: NotificationPermission = "granted";
    static requestPermission = jest.fn(async () => {
        MockNotification.permission = "granted";
        return MockNotification.permission;
    });

    onclick: (() => void) | null = null;
    close = jest.fn();

    constructor(public title: string, public options: NotificationOptions) {
        instances.push(this);
    }
}

const getTestDocument = () => globalThis.document as Document;
const getTestWindow = () => globalThis.window as Window & typeof globalThis;

const setPageState = (visibilityState: DocumentVisibilityState, focused: boolean) => {
    Object.defineProperty(getTestDocument(), "visibilityState", {
        configurable: true,
        value: visibilityState,
    });
    Object.defineProperty(getTestDocument(), "hasFocus", {
        configurable: true,
        value: jest.fn(() => focused),
    });
};

describe("browser notifications", () => {
    beforeEach(() => {
        instances.length = 0;
        MockNotification.permission = "granted";
        MockNotification.requestPermission.mockClear();
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: {},
        });
        Object.defineProperty(globalThis, "window", {
            configurable: true,
            value: {
                location: { assign: jest.fn() },
                focus: jest.fn(),
            },
        });
        Object.defineProperty(getTestWindow(), "Notification", {
            configurable: true,
            value: MockNotification,
        });
        setPageState("hidden", false);
        resetBrowserNotificationsForTests();
    });

    it("reports and requests browser permission", async () => {
        MockNotification.permission = "default";

        expect(getBrowserNotificationPermission()).toBe("default");
        await expect(requestBrowserNotificationPermission()).resolves.toBe("granted");
        expect(MockNotification.requestPermission).toHaveBeenCalledTimes(1);
    });

    it("does not show messages while the page is active", () => {
        setPageState("visible", true);

        const shown = showMessageBrowserNotification({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "peer-1",
            body: "hello",
            attachments: [],
            state: "Sent",
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        });

        expect(shown).toBe(false);
        expect(instances).toHaveLength(0);
    });

    it("shows hidden-tab message notifications and routes clicks", () => {
        const navigate = jest.fn();
        setBrowserNotificationNavigationForTests(navigate);

        const shown = showMessageBrowserNotification(
            {
                id: "message-1",
                conversationId: "direct-1",
                senderId: "peer-1",
                body: "hello",
                attachments: [],
                state: "Sent",
                createdAt: "2026-05-24T10:03:00.000Z",
                deliveredAt: null,
                readAt: null,
            },
            { currentUserId: "user-1", title: "Peer" },
        );

        expect(shown).toBe(true);
        expect(instances).toHaveLength(1);
        expect(instances[0].title).toBe("Peer");
        expect(instances[0].options.body).toBe("hello");

        instances[0].onclick?.();

        expect(getTestWindow().focus).toHaveBeenCalled();
        expect(navigate).toHaveBeenCalledWith("/chats/direct-1?message=message-1");
        expect(instances[0].close).toHaveBeenCalled();
    });

    it("deduplicates and skips own or muted message notifications", () => {
        configureBrowserNotifications({
            newMessages: true,
            notificationSound: true,
            disciplineChatMessages: true,
            mentions: true,
            mutedConversationIds: ["muted-1"],
        });

        const message = {
            id: "message-1",
            conversationId: "direct-1",
            senderId: "peer-1",
            body: "hello",
            attachments: [],
            state: "Sent" as const,
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        };

        expect(showMessageBrowserNotification(message, { currentUserId: "user-1" })).toBe(true);
        expect(showMessageBrowserNotification(message, { currentUserId: "user-1" })).toBe(false);
        expect(
            showMessageBrowserNotification(
                { ...message, id: "own-1", senderId: "user-1" },
                { currentUserId: "user-1" },
            ),
        ).toBe(false);
        expect(
            showMessageBrowserNotification(
                { ...message, id: "muted-message", conversationId: "muted-1" },
                { currentUserId: "user-1" },
            ),
        ).toBe(false);

        expect(instances).toHaveLength(1);
    });

    it("shows server notifications with deep-link routing and mute checks", () => {
        configureBrowserNotifications({
            newMessages: true,
            notificationSound: true,
            disciplineChatMessages: true,
            mentions: true,
            mutedConversationIds: ["muted-1"],
        });

        const baseNotification: NotificationDto = {
            id: "notification-1",
            recipientUserId: "user-1",
            type: "chat.mention",
            category: 2,
            severity: 2,
            title: "Вас упомянули",
            body: "Вас упомянули в чате",
            imageUrl: null,
            deepLink: "urfulink://chat/conv/direct-1/msg/aaaaaaaa111122223333bbbbbbbbbbbb",
            data: {
                conversationId: "direct-1",
                messageId: "aaaaaaaa111122223333bbbbbbbbbbbb",
            },
            actor: null,
            entity: null,
            actions: [],
            groupKey: null,
            occurrenceCount: 1,
            createdAtUtc: "2026-05-24T10:03:00.000Z",
            lastOccurrenceAtUtc: "2026-05-24T10:03:00.000Z",
            readAtUtc: null,
            seenAtUtc: null,
            savedAtUtc: null,
            doneAtUtc: null,
            archivedAtUtc: null,
            snoozedUntilUtc: null,
            expiresAtUtc: null,
        };

        expect(showServerBrowserNotification(baseNotification)).toBe(true);
        expect(
            showServerBrowserNotification({
                ...baseNotification,
                id: "notification-2",
                data: { conversationId: "muted-1", messageId: "message-2" },
            }),
        ).toBe(false);

        expect(instances).toHaveLength(1);
        expect(instances[0].options.tag).toContain("notification:notification-1");
    });

    it("deduplicates chat and server notifications for the same message", () => {
        const message = {
            id: "message-1",
            conversationId: "direct-1",
            senderId: "peer-1",
            body: "hello",
            attachments: [],
            state: "Sent" as const,
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        };

        const notification: NotificationDto = {
            id: "notification-1",
            recipientUserId: "user-1",
            type: "chat.mention",
            category: 2,
            severity: 2,
            title: "Вас упомянули",
            body: "hello",
            imageUrl: null,
            deepLink: "urfulink://chat/conv/direct-1/msg/message-1",
            data: {
                conversationId: "direct-1",
                messageId: "message-1",
            },
            actor: null,
            entity: null,
            actions: [],
            groupKey: null,
            occurrenceCount: 1,
            createdAtUtc: "2026-05-24T10:03:00.000Z",
            lastOccurrenceAtUtc: "2026-05-24T10:03:00.000Z",
            readAtUtc: null,
            seenAtUtc: null,
            savedAtUtc: null,
            doneAtUtc: null,
            archivedAtUtc: null,
            snoozedUntilUtc: null,
            expiresAtUtc: null,
        };

        expect(showMessageBrowserNotification(message, { currentUserId: "user-1" })).toBe(true);
        expect(showServerBrowserNotification(notification)).toBe(false);
        expect(showServerBrowserNotification({ ...notification, id: "notification-2" })).toBe(false);
        expect(instances).toHaveLength(1);
    });

    it("routes server call notifications to the source chat when conversation data is available", () => {
        const navigate = jest.fn();
        setBrowserNotificationNavigationForTests(navigate);

        const notification: NotificationDto = {
            id: "notification-call",
            recipientUserId: "user-1",
            type: "call.missed",
            category: 11,
            severity: 1,
            title: "Пропущенный звонок",
            body: "Звонок остался без ответа",
            imageUrl: null,
            deepLink: "urfulink://call/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/missed",
            data: {
                conversationId: "direct-1",
                callId: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            },
            actor: null,
            entity: null,
            actions: [],
            groupKey: null,
            occurrenceCount: 1,
            createdAtUtc: "2026-05-24T10:03:00.000Z",
            lastOccurrenceAtUtc: "2026-05-24T10:03:00.000Z",
            readAtUtc: null,
            seenAtUtc: null,
            savedAtUtc: null,
            doneAtUtc: null,
            archivedAtUtc: null,
            snoozedUntilUtc: null,
            expiresAtUtc: null,
        };

        expect(showServerBrowserNotification(notification)).toBe(true);

        instances[0].onclick?.();

        expect(navigate).toHaveBeenCalledWith("/chats/direct-1");
        expect(navigate).not.toHaveBeenCalledWith(expect.stringContaining("/call/"));
    });

    it("shows stale server call notifications without opening the call screen", () => {
        const navigate = jest.fn();
        setBrowserNotificationNavigationForTests(navigate);

        const notification: NotificationDto = {
            id: "notification-call",
            recipientUserId: "user-1",
            type: "call.missed",
            category: 11,
            severity: 1,
            title: "Пропущенный звонок",
            body: "Звонок остался без ответа",
            imageUrl: null,
            deepLink: "urfulink://call/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/missed",
            data: {
                callId: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            },
            actor: null,
            entity: null,
            actions: [],
            groupKey: null,
            occurrenceCount: 1,
            createdAtUtc: "2026-05-24T10:03:00.000Z",
            lastOccurrenceAtUtc: "2026-05-24T10:03:00.000Z",
            readAtUtc: null,
            seenAtUtc: null,
            savedAtUtc: null,
            doneAtUtc: null,
            archivedAtUtc: null,
            snoozedUntilUtc: null,
            expiresAtUtc: null,
        };

        expect(showServerBrowserNotification(notification)).toBe(true);

        instances[0].onclick?.();

        expect(navigate).not.toHaveBeenCalled();
    });
});
