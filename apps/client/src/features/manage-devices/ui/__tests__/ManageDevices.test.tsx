import { render } from "@testing-library/react-native";
import { ScrollView } from "react-native";

import { ManageDevices } from "../ManageDevices";

jest.mock("@/entities/user", () => ({
    useDevices: () => ({
        data: [
            {
                sessionId: "session-1",
                os: "Web",
                browser: "Chrome, Windows",
                ipAddress: "95.142.87.140",
                lastAccess: "2026-05-31T12:00:00.000Z",
                isCurrent: true,
            },
        ],
        isLoading: false,
    }),
    useTerminateAllDevices: () => ({ mutate: jest.fn() }),
    useTerminateDevice: () => ({ mutate: jest.fn() }),
}));

jest.mock("@/shared/ui", () => ({
    Button: () => {
        const { Text } = require("react-native");
        return <Text>button</Text>;
    },
    EmptyState: () => {
        const { Text } = require("react-native");
        return <Text>empty</Text>;
    },
    Skeleton: () => {
        const { View } = require("react-native");
        return <View />;
    },
}));

jest.mock("@/shared/ui/phosphor", () => ({
    DeviceMobileIcon: () => null,
    LaptopIcon: () => null,
}));

describe("ManageDevices", () => {
    it("keeps scroll content padded so card glow is not clipped", () => {
        const { UNSAFE_getByType } = render(<ManageDevices />);

        expect(UNSAFE_getByType(ScrollView).props.contentContainerClassName).toContain("px-1 py-1");
    });
});
