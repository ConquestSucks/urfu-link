import { fireEvent, render, screen } from "@testing-library/react-native";

import { InboxLayout } from "../InboxLayout";

const mockLoadConversations = jest.fn();
const mockFetchNotifications = jest.fn();

jest.mock("expo-router", () => ({
    router: {
        push: jest.fn(),
    },
    useSegments: () => ["(authorized)", "chats", "[id]"],
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("@/shared/lib/useInboxRouting", () => ({
    useInboxRouting: () => ({
        currentTab: "chats",
        currentView: "messages",
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
    InboxNotification: () => null,
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: unknown) => unknown) =>
        selector({
            isConversationsLoading: false,
            loadConversations: mockLoadConversations,
        }),
}));

jest.mock("@/shared/store/useInboxStore", () => ({
    useInboxStore: (selector: (state: unknown) => unknown) =>
        selector({
            notifications: [],
            isNotificationsLoading: false,
            fetchNotifications: mockFetchNotifications,
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
    });

    it("reopens a chat even if the row is still marked active", () => {
        const { router } = require("expo-router");
        render(<InboxLayout />);

        fireEvent.press(screen.getByTestId("inbox-chat-row"));

        expect(router.push).toHaveBeenCalledWith("/chats/chat-1");
    });
});
