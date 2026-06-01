import { render, screen } from "@testing-library/react-native";
import type React from "react";
import { StyleSheet } from "react-native";

import { MobileBottomTabs } from "../MobileBottomTabs";

let mockPathname = "/chats";
let mockSegments = ["(authorized)", "chats"];
const mockReplace = jest.fn();

jest.mock("expo-router", () => ({
    router: {
        replace: (...args: unknown[]) => mockReplace(...args),
    },
    usePathname: () => mockPathname,
    useSegments: () => mockSegments,
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
    });

    it("hides tabs when nested chat detail is active even if pathname is still the list route", () => {
        mockSegments = ["(authorized)", "chats", "[id]"];

        render(<MobileBottomTabs />);

        const style = StyleSheet.flatten(screen.getByTestId("mobile-bottom-tabs-container").props.style);
        expect(style.pointerEvents).toBe("none");
    });

    it("keeps tabs interactive on the inbox list route", () => {
        render(<MobileBottomTabs />);

        const style = StyleSheet.flatten(screen.getByTestId("mobile-bottom-tabs-container").props.style);
        expect(style.pointerEvents).toBe("auto");
    });
});
