import { render, screen } from "@testing-library/react-native";
import { Avatar } from "../Avatar";

jest.mock("expo-image", () => ({
    Image: ({ accessibilityLabel, source }: { accessibilityLabel?: string; source?: unknown }) => {
        const { View } = require("react-native");
        return (
            <View
                testID="avatar-image"
                accessibilityLabel={accessibilityLabel}
                {...({ source } as any)}
            />
        );
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

    it("selects the closest generated avatar variant for small images", () => {
        render(
            <Avatar
                src="https://storage.test/user-avatars/avatars/user/avatar-256.webp"
                name="Ivan Petrov"
                size={40}
            />,
        );

        expect(screen.getByTestId("avatar-image").props.source).toEqual({
            uri: "https://storage.test/user-avatars/avatars/user/avatar-64.webp",
        });
    });

    it("keeps legacy avatar URLs unchanged", () => {
        render(<Avatar src="https://storage.test/user-avatars/avatars/user/avatar.png" size={40} />);

        expect(screen.getByTestId("avatar-image").props.source).toEqual({
            uri: "https://storage.test/user-avatars/avatars/user/avatar.png",
        });
    });
});
