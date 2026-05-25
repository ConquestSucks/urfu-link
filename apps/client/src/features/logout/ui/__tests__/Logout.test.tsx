import { fireEvent, render, screen } from "@testing-library/react-native";

import { Logout } from "../Logout";
import { performLogout } from "../../model/logout";

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("@/shared/ui", () => {
    const { Pressable, Text } = require("react-native");

    return {
        Button: ({ label, onPress }: { label?: string; onPress: () => void }) => (
            <Pressable onPress={onPress}>
                <Text>{label}</Text>
            </Pressable>
        ),
    };
});

jest.mock("@/shared/ui/phosphor", () => ({
    SignOutIcon: () => null,
}));

jest.mock("../../model/logout", () => ({
    performLogout: jest.fn(),
}));

describe("Logout", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("starts logout when the button is pressed", () => {
        render(<Logout />);

        fireEvent.press(screen.getByText("Выйти"));

        expect(performLogout).toHaveBeenCalledTimes(1);
    });
});
