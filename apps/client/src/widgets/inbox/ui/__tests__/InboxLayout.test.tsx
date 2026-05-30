import { fireEvent, render, screen } from "@testing-library/react-native";

import { InboxLayout } from "../InboxLayout";

const mockLoadConversations = jest.fn();
const mockFetchNotifications = jest.fn();
const mockMarkNotificationRead = jest.fn();
const mockMarkAllNotificationsRead = jest.fn();
const mockSetPendingScrollToMessageId = jest.fn();
const mockRouterPush = jest.fn();
let mockCurrentView: "messages" | "notifications" = "messages";
let mockCurrentTab: "chats" | "subjects" = "chats";
let mockNotifications: any[] = [
    {
        id: "notification-1",
        title: "Новое сообщение",
        description: "Иван написал вам",
        time: "12:10",
        scope: "chats",
        deepLink:
            "urfulink://chat/conv/direct-1/msg/3f7d3e57b4f5481e93a1c8e9b4d70a11",
        data: {
            conversationId: "direct-1",
            messageId: "3f7d3e57b4f5481e93a1c8e9b4d70a11",
        },
        isRead: false,
    },
];

jest.mock("expo-router", () => ({
    router: {
        push: (...args: unknown[]) => mockRouterPush(...args),
    },
    useSegments: () => ["(authorized)", "chats", "[id]"],
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("@/shared/lib/useInboxRouting", () => ({
    useInboxRouting: () => ({
        currentTab: mockCurrentTab,
        currentView: mockCurrentView,
        params: { id: "chat-1" },
    }),
}));

jest.mock("@/shared/ui", () => ({
    MasterDetailLayout: ({ sidebar }: { sidebar: React.ReactNode }) => {
        const { View } = require("react-native");
        return <View>{sidebar}</View>;
    },
}));

jest.mock("@/entities/inbox-chat", () => ({
    InboxChat: ({ name, onPress }: { name: string; onPress: () => void }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="inbox-chat-row" onPress={onPress}>
                <Text>{name}</Text>
            </Pressable>
        );
    },
}));

jest.mock("@/entities/inbox-notification", () => ({
    InboxNotification: ({
        title,
        onPress,
    }: {
        title: string;
        onPress: () => void;
    }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="inbox-notification-row" onPress={onPress}>
                <Text>{title}</Text>
            </Pressable>
        );
    },
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: unknown) => unknown) =>
        selector({
            isConversationsLoading: false,
            loadConversations: mockLoadConversations,
            setPendingScrollToMessageId: mockSetPendingScrollToMessageId,
        }),
}));

jest.mock("@/shared/store/useInboxStore", () => ({
    useInboxStore: (selector: (state: unknown) => unknown) =>
        selector({
            notifications: mockNotifications,
            isNotificationsLoading: false,
            isMarkingAllNotificationsRead: false,
            fetchNotifications: mockFetchNotifications,
            markNotificationRead: mockMarkNotificationRead,
            markAllNotificationsRead: mockMarkAllNotificationsRead,
        }),
}));

jest.mock("../../model/use-inbox-conversations", () => ({
    useInboxConversations: (tab: "chats" | "subjects") =>
        tab === "chats"
            ? [
                  {
                      id: "chat-1",
                      avatarUrl: "",
                      name: "Peer User",
                      message: "Привет",
                      time: "12:00",
                  },
              ]
            : [],
}));

jest.mock("../Inbox", () => ({
    Inbox: ({ data, renderItem }: { data: unknown[]; renderItem: (item: unknown) => React.ReactNode }) => {
        const { View } = require("react-native");
        return <View>{data.map(renderItem)}</View>;
    },
}));

jest.mock("../InboxMobile", () => ({
    InboxMobile: () => null,
}));

describe("InboxLayout navigation", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockCurrentView = "messages";
        mockCurrentTab = "chats";
        mockNotifications = [
            {
                id: "notification-1",
                title: "Новое сообщение",
                description: "Иван написал вам",
                time: "12:10",
                scope: "chats",
                deepLink:
                    "urfulink://chat/conv/direct-1/msg/3f7d3e57b4f5481e93a1c8e9b4d70a11",
                data: {
                    conversationId: "direct-1",
                    messageId: "3f7d3e57b4f5481e93a1c8e9b4d70a11",
                },
                isRead: false,
            },
        ];
    });

    it("reopens a chat even if the row is still marked active", () => {
        render(<InboxLayout />);

        fireEvent.press(screen.getByTestId("inbox-chat-row"));

        expect(mockRouterPush).toHaveBeenCalledWith("/chats/chat-1");
    });

    it("opens a notification deep link and prepares message scrolling", () => {
        mockCurrentView = "notifications";

        render(<InboxLayout />);

        fireEvent.press(screen.getByTestId("inbox-notification-row"));

        expect(mockMarkNotificationRead).toHaveBeenCalledWith("notification-1");
        expect(mockSetPendingScrollToMessageId).toHaveBeenCalledWith(
            "3f7d3e57-b4f5-481e-93a1-c8e9b4d70a11",
        );
        expect(mockRouterPush).toHaveBeenCalledWith(
            "/chats/direct-1?message=3f7d3e57-b4f5-481e-93a1-c8e9b4d70a11",
        );
    });

    it("marks stale call notifications read without opening a call screen", () => {
        mockCurrentView = "notifications";
        mockNotifications = [
            {
                id: "notification-call",
                title: "Пропущенный звонок",
                description: "Звонок остался без ответа",
                time: "12:10",
                scope: "chats",
                deepLink: "urfulink://call/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/missed",
                data: {
                    callId: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                },
                isRead: false,
            },
        ];

        render(<InboxLayout />);

        fireEvent.press(screen.getByTestId("inbox-notification-row"));

        expect(mockMarkNotificationRead).toHaveBeenCalledWith("notification-call");
        expect(mockSetPendingScrollToMessageId).not.toHaveBeenCalled();
        expect(mockRouterPush).not.toHaveBeenCalled();
    });
});
