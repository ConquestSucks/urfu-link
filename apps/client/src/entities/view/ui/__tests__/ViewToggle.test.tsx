import { render, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { ViewToggle } from "../ViewToggle";

const mockLink = jest.fn(({ children }: { children: React.ReactNode }) => children);

jest.mock("expo-router", () => ({
    Link: (props: { children: React.ReactNode }) => mockLink(props),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isDesktop: false, isMobile: true, width: 390 }),
}));

jest.mock("@/features/notifications", () => ({
    getNotificationScopeUnreadCount: () => 0,
    useNotificationBadge: () => ({ data: null }),
}));

jest.mock("@/shared/store/notification-store", () => ({
    useNotificationStore: () => null,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        BellIcon: makeIcon("bell-icon"),
        ChatCircleTextIcon: makeIcon("chat-icon"),
    };
});

describe("ViewToggle", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("uses a rounded fill instead of a shadow for active mobile icons", () => {
        render(
            <ViewToggle
                currentView="messages"
                createHref={(view) => ({ pathname: "/chats", params: { view } })}
            />,
        );

        const activeButton = screen.getByTestId("view-toggle-messages");
        const activeStyle = StyleSheet.flatten(activeButton.props.style);

        expect(activeButton.props.className).toContain("rounded-full");
        expect(activeButton.props.className).toContain("bg-brand-650/12");
        expect(activeStyle?.boxShadow).toBeUndefined();
    });
});
