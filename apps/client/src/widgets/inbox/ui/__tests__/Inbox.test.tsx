import { fireEvent, render, screen } from "@testing-library/react-native";
import { Text } from "react-native";

import { Inbox } from "../Inbox";
import { InboxMobile } from "../InboxMobile";

let mockCurrentView: "messages" | "notifications" = "messages";

jest.mock("@/shared/lib/useInboxRouting", () => ({
    useInboxRouting: () => ({
        currentTab: "chats",
        currentView: mockCurrentView,
        createTabHref: jest.fn(),
        createViewHref: jest.fn(),
        params: {},
    }),
}));

jest.mock("@/entities/inbox-chat", () => ({
    InboxChatSkeleton: () => {
        const { Text: MockText } = require("react-native");
        return <MockText testID="chat-skeleton">chat skeleton</MockText>;
    },
}));

jest.mock("@/entities/inbox-notification", () => ({
    InboxNotificationSkeleton: () => {
        const { Text: MockText } = require("react-native");
        return (
            <MockText testID="notification-skeleton">
                notification skeleton
            </MockText>
        );
    },
}));

jest.mock("@/entities/inbox-subject", () => ({
    InboxSubjectSkeleton: () => {
        const { Text: MockText } = require("react-native");
        return <MockText testID="subject-skeleton">subject skeleton</MockText>;
    },
}));

jest.mock("@/shared/ui", () => ({
    EmptyState: () => {
        const { Text: MockText } = require("react-native");
        return <MockText testID="empty-state">empty</MockText>;
    },
    SearchBar: () => {
        const { Text: MockText } = require("react-native");
        return <MockText>search</MockText>;
    },
}));

jest.mock("@/widgets/header-mobile", () => ({
    MobileHeader: () => {
        const { Text: MockText } = require("react-native");
        return <MockText>mobile header</MockText>;
    },
}));

jest.mock("../Header", () => ({
    Header: ({ title }: { title: string }) => {
        const { Text: MockText } = require("react-native");
        return <MockText>{title}</MockText>;
    },
}));

jest.mock("../InboxTabsMobile", () => ({
    InboxTabsMobile: () => {
        const { Text: MockText } = require("react-native");
        return <MockText>tabs</MockText>;
    },
}));

jest.mock("@/features/chat-search", () => ({
    GlobalSearchPanel: () => {
        const { Text: MockText } = require("react-native");
        return <MockText>global search</MockText>;
    },
    useGlobalSearch: () => ({ onQueryChange: jest.fn() }),
    useSearchStore: (selector: (state: { globalQuery: string }) => unknown) =>
        selector({ globalQuery: "" }),
}));

type Row = {
    id: string;
    name: string;
};

const rows: Row[] = [{ id: "chat-1", name: "Dev Teacher" }];

const renderItem = (item: Row) => <Text>{item.name}</Text>;

describe("Inbox loading state", () => {
    beforeEach(() => {
        mockCurrentView = "messages";
    });

    it("keeps desktop rows visible during background loading", () => {
        render(<Inbox data={rows} isLoading renderItem={renderItem} />);

        expect(screen.getByText("Dev Teacher")).toBeTruthy();
        expect(screen.queryByTestId("chat-skeleton")).toBeNull();
    });

    it("keeps mobile rows visible during background loading", () => {
        render(<InboxMobile data={rows} isLoading renderItem={renderItem} />);

        expect(screen.getByText("Dev Teacher")).toBeTruthy();
        expect(screen.queryByTestId("chat-skeleton")).toBeNull();
    });

    it("shows mark-all-read action for desktop notification list", () => {
        mockCurrentView = "notifications";
        const onMarkAllNotificationsRead = jest.fn();

        render(
            <Inbox
                data={rows}
                renderItem={renderItem}
                notificationUnreadCount={2}
                onMarkAllNotificationsRead={onMarkAllNotificationsRead}
            />,
        );

        fireEvent.press(screen.getByText("Прочитать все"));

        expect(onMarkAllNotificationsRead).toHaveBeenCalledTimes(1);
    });
});
