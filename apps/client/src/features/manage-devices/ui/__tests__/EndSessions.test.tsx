import { render, screen } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { EndSessions } from "../EndSessions";

jest.mock("@/shared/ui", () => ({
    Button: () => {
        const { Text } = require("react-native");
        return <Text>button</Text>;
    },
}));

describe("EndSessions", () => {
    it("uses restrained danger glow", () => {
        render(<EndSessions onPress={jest.fn()} />);

        const style = StyleSheet.flatten(screen.getByTestId("end-sessions-card").props.style);

        expect(style.boxShadow).toBe("0 0 12px rgba(251, 44, 54, 0.12)");
    });
});
