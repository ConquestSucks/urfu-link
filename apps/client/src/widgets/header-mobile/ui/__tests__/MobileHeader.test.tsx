import { render } from "@testing-library/react-native";

import { MobileHeader } from "../MobileHeader";

const mockLink = jest.fn(({ children }: { children: React.ReactNode }) => children);

jest.mock("expo-router", () => ({
    Link: (props: { children: React.ReactNode }) => mockLink(props),
}));

jest.mock("@/entities/view", () => ({
    ViewToggle: () => null,
}));

jest.mock("@/shared/ui", () => {
    const { View } = require("react-native");
    return {
        Logo: ({ size }: { size: number }) => (
            <View testID="mobile-logo" style={{ height: size, width: size }} />
        ),
    };
});

describe("MobileHeader", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("links the logo brand area to the home inbox", () => {
        render(<MobileHeader createHref={jest.fn()} />);

        expect(mockLink).toHaveBeenCalledWith(expect.objectContaining({
            asChild: true,
            href: {
                pathname: "/chats",
                params: { view: "messages" },
            },
            replace: true,
        }));
    });
});
