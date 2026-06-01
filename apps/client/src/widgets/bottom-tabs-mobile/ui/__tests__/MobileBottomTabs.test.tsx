import { fireEvent, render, screen } from "@testing-library/react-native";
import type React from "react";

import { MobileBottomTabs } from "../MobileBottomTabs";

let mockPathname = "/chats";
const mockReplace = jest.fn();

jest.mock("expo-router", () => ({
    router: {
        replace: (...args: unknown[]) => mockReplace(...args),
    },
    usePathname: () => mockPathname,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    return {
        ChatCircleTextIcon: () => <Text>chat-icon</Text>,
        UserCircleIcon: () => <Text>profile-icon</Text>,
    };
});

describe("MobileBottomTabs", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockPathname = "/chats";
    });

    it("renders as a static footer without route-driven hiding", () => {
        render(<MobileBottomTabs />);

        expect(screen.getByTestId("mobile-bottom-tabs-container")).toBeTruthy();
        expect(screen.getByText("chat-icon")).toBeTruthy();
        expect(screen.getByText("profile-icon")).toBeTruthy();
    });

    it("treats subject routes as part of the chats tab", () => {
        mockPathname = "/subjects";

        render(<MobileBottomTabs />);

        const iconPills = screen.getAllByTestId("mobile-bottom-tab-icon-pill");
        expect(iconPills[0].props.className).toContain("bg-brand-650/12");
    });

    it("replaces the route when a tab is pressed", () => {
        render(<MobileBottomTabs />);

        fireEvent.press(screen.getByText("profile-icon"));

        expect(mockReplace).toHaveBeenCalledWith("/profile");
    });
});
