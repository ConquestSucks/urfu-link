import { render, screen } from "@testing-library/react-native";
import { StyleSheet, Text } from "react-native";

import { MobileBottomTabItem } from "../MobileBottomTabItem";

const Icon = () => <Text>icon</Text>;

describe("MobileBottomTabItem", () => {
    it("does not put a shadow behind the active icon pill", () => {
        render(
            <MobileBottomTabItem
                icon={Icon}
                label="Chats"
                isActive
                onPress={jest.fn()}
            />,
        );

        const iconPillStyle = StyleSheet.flatten(
            screen.getByTestId("mobile-bottom-tab-icon-pill").props.style,
        );

        expect(iconPillStyle?.boxShadow).toBeUndefined();
    });
});
