import { render } from "@testing-library/react-native";
import { StyleSheet } from "react-native";

import { Header } from "../Header";

const mockLink = jest.fn(({ children }: { children: React.ReactNode }) => children);

jest.mock("expo-router", () => ({
    Link: (props: { children: React.ReactNode }) => mockLink(props),
}));

jest.mock("@/shared/lib/nativewind-interop", () => {
    const { View } = require("react-native");
    return { AnimatedView: View };
});

jest.mock("@/shared/ui", () => {
    const { View } = require("react-native");
    return {
        Logo: ({ size }: { size: number }) => (
            <View testID="desktop-logo" style={{ height: size, width: size }} />
        ),
    };
});

describe("Sidebar desktop Header", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("links the logo brand area to the home inbox", () => {
        render(<Header textAnimatedStyle={{}} />);

        expect(mockLink).toHaveBeenCalledWith(expect.objectContaining({
            asChild: true,
            href: {
                pathname: "/chats",
                params: { view: "messages" },
            },
            replace: true,
        }));
    });

    it("uses a circular transparent halo for the logo highlight", () => {
        const { getByTestId } = render(<Header textAnimatedStyle={{}} />);

        const style = StyleSheet.flatten(getByTestId("desktop-logo-halo").props.style);

        expect(style).toEqual(expect.objectContaining({
            alignItems: "center",
            backgroundColor: "transparent",
            borderRadius: 19.5,
            height: 39,
            justifyContent: "center",
            overflow: "visible",
            width: 39,
        }));
    });
});
