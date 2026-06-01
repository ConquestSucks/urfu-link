import { render, screen } from "@testing-library/react-native";
import { ScrollView } from "react-native";

import ProfileGroupLayout from "../_layout";

let mockPathname = "/profile";
const mockBack = jest.fn();
const mockPush = jest.fn();

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: true, isDesktop: false, width: 390 }),
}));

jest.mock("@/widgets/bottom-tabs-mobile", () => ({
    MobileBottomTabs: () => {
        const { View } = require("react-native");
        return <View testID="mobile-bottom-tabs-container" />;
    },
}));

jest.mock("@/shared/ui/phosphor", () => ({
    CaretLeftIcon: () => null,
}));

jest.mock("expo-router", () => ({
    Redirect: ({ href }: { href: string }) => {
        const { Text } = require("react-native");
        return <Text>{`redirect:${href}`}</Text>;
    },
    Slot: () => {
        const { Text } = require("react-native");
        return <Text>profile slot</Text>;
    },
    usePathname: () => mockPathname,
    useRouter: () => ({
        back: mockBack,
        canGoBack: () => false,
        push: mockPush,
    }),
}));

describe("ProfileGroupLayout", () => {
    beforeEach(() => {
        mockPathname = "/profile";
        jest.clearAllMocks();
    });

    it("renders mobile bottom tabs as a footer without reserving overlay space", () => {
        render(<ProfileGroupLayout />);

        expect(screen.getByTestId("mobile-bottom-tabs-container")).toBeTruthy();
        const scrollView = screen.UNSAFE_getByType(ScrollView);
        expect(scrollView.props.contentContainerStyle).toEqual({ paddingBottom: 24 });
    });
});
