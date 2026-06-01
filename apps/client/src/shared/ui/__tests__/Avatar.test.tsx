import { render, screen } from "@testing-library/react-native";
import { Avatar } from "../Avatar";

jest.mock("expo-image", () => ({
    Image: ({
        accessibilityLabel,
        className,
        source,
        style,
    }: {
        accessibilityLabel?: string;
        className?: string;
        source?: unknown;
        style?: unknown;
    }) => {
        const { View } = require("react-native");
        return (
            <View
                testID="avatar-image"
                accessibilityLabel={accessibilityLabel}
                className={className}
                style={style}
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

    it("renders image avatars with the web-style rounded square shape on native", () => {
        render(<Avatar src="https://cdn.test/avatar.webp" size={48} />);

        expect(screen.getByTestId("avatar-image").props.className).toContain("rounded-xl");
        expect(screen.getByTestId("avatar-image").props.style).toEqual(
            expect.objectContaining({
                width: 48,
                height: 48,
                borderRadius: 12,
            }),
        );
    });

    it("renders generated initials avatars with the web-style rounded square shape on native", () => {
        render(<Avatar name="Ivan Petrov" size={48} />);

        expect(screen.getByTestId("avatar-initials").props.className).toContain("rounded-xl");
        expect(screen.getByTestId("avatar-initials").props.style).toEqual(
            expect.objectContaining({
                width: 48,
                height: 48,
                borderRadius: 12,
            }),
        );
    });

    it("keeps explicit circular avatar overrides", () => {
        render(<Avatar src="https://cdn.test/avatar.webp" size={48} className="rounded-full" />);

        expect(screen.getByTestId("avatar-image").props.style).toEqual(
            expect.objectContaining({
                width: 48,
                height: 48,
                borderRadius: 24,
            }),
        );
    });
});
