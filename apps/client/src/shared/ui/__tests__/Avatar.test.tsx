import { render, screen } from "@testing-library/react-native";
import { Avatar } from "../Avatar";

jest.mock("expo-image", () => ({
    Image: ({ accessibilityLabel }: { accessibilityLabel?: string }) => {
        const { View } = require("react-native");
        return <View testID="avatar-image" accessibilityLabel={accessibilityLabel} />;
    },
}));

describe("Avatar accessibility", () => {
    it("uses the display name as the default image label", () => {
        render(<Avatar src="https://cdn.test/avatar.webp" name="Ivan Petrov" />);

        expect(screen.getByTestId("avatar-image").props.accessibilityLabel).toBe(
            "Аватар Ivan Petrov",
        );
    });

    it("uses an empty image label for decorative avatars", () => {
        render(<Avatar src="https://cdn.test/avatar.webp" name="Ivan Petrov" decorative />);

        expect(screen.getByTestId("avatar-image").props.accessibilityLabel).toBe("");
    });
});
