import { render, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { DeviceCard } from "../DeviceCard";

jest.mock("@/shared/ui", () => ({
    Button: () => {
        const { Text } = require("react-native");
        return <Text>button</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => ({
    DeviceMobileIcon: () => null,
    LaptopIcon: () => null,
}));

describe("DeviceCard", () => {
    it("uses restrained glow for the current device card", () => {
        render(
            <DeviceCard
                platform="Web"
                name="Chrome, Windows"
                location="95.142.87.140"
                lastLogin="Now active"
                isActive
                onPress={jest.fn()}
            />,
        );

        const cardStyle = StyleSheet.flatten(screen.getByTestId("device-card").props.style);
        const iconStyle = StyleSheet.flatten(screen.getByTestId("device-card-icon").props.style);
        const badgeStyle = StyleSheet.flatten(screen.getByTestId("device-card-current-badge").props.style);

        expect(cardStyle.boxShadow).toBe("0 0 0 1px rgba(0, 212, 146, 0.14)");
        expect(iconStyle.boxShadow).toBe("0 0 0 1px rgba(0, 212, 146, 0.14)");
        expect(badgeStyle?.boxShadow).toBeUndefined();
    });
});
