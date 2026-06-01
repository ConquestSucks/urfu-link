import { render, screen } from "@testing-library/react-native";
import type React from "react";
import { StyleSheet } from "react-native";

import { MobileBottomTabs } from "../MobileBottomTabs";

let mockPathname = "/chats";
let mockSegments = ["(authorized)", "chats"];
let mockLocalParams: Record<string, string | undefined> = {};
const mockReplace = jest.fn();

jest.mock("expo-router", () => ({
    router: {
        replace: (...args: unknown[]) => mockReplace(...args),
    },
    usePathname: () => mockPathname,
    useSegments: () => mockSegments,
    useLocalSearchParams: () => mockLocalParams,
}));

jest.mock("@/shared/lib/nativewind-interop", () => ({
    AnimatedView: ({
        children,
        style,
        testID,
    }: {
        children?: React.ReactNode;
        style?: unknown;
        testID?: string;
    }) => {
        const { View } = require("react-native");
        return (
        <View testID={testID} style={style}>
            {children}
        </View>
        );
    },
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
        mockSegments = ["(authorized)", "chats"];
        mockLocalParams = {};
    });

    it("hides tabs when nested chat detail is active even if pathname is still the list route", () => {
        mockSegments = ["(authorized)", "chats", "[id]"];
        mockLocalParams = { id: "chat-1" };

        render(<MobileBottomTabs />);

        const style = StyleSheet.flatten(screen.getByTestId("mobile-bottom-tabs-container").props.style);
        expect(style.pointerEvents).toBe("none");
    });

    it("shows tabs on the inbox root when only stale dynamic segments remain", () => {
        mockSegments = ["(authorized)", "chats", "[id]"];

        render(<MobileBottomTabs />);

        const style = StyleSheet.flatten(screen.getByTestId("mobile-bottom-tabs-container").props.style);
        expect(style.pointerEvents).toBe("auto");
    });

    it("keeps tabs interactive on the inbox list route", () => {
        render(<MobileBottomTabs />);

        const style = StyleSheet.flatten(screen.getByTestId("mobile-bottom-tabs-container").props.style);
        expect(style.pointerEvents).toBe("auto");
    });
});
