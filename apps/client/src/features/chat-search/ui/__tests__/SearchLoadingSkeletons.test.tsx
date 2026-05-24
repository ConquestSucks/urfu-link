import { render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { UserSearchResults } from "../UserSearchResults";

const mockClearGlobal = jest.fn();
let mockUserSearchState: unknown;

jest.mock("react-native-reanimated", () => {
    const { View } = require("react-native");
    return {
        Easing: { linear: jest.fn() },
        interpolate: jest.fn(),
        useAnimatedStyle: () => ({}),
        useSharedValue: (value: unknown) => ({ value }),
        withRepeat: (value: unknown) => value,
        withTiming: (value: unknown) => value,
        default: {
            View,
            Text: require("react-native").Text,
            createAnimatedComponent: (component: unknown) => component,
        },
    };
});

jest.mock("@/shared/lib/nativewind-interop", () => ({
    AnimatedView: ({ children }: { children?: ReactNode }) => {
        const { View } = require("react-native");
        return <View>{children}</View>;
    },
}));

jest.mock("../UserSearchResultItem", () => ({
    UserSearchResultItem: () => {
        const { Text } = require("react-native");
        return <Text>result item</Text>;
    },
}));

jest.mock("../../model/use-search", () => ({
    useUserSearch: () => mockUserSearchState,
}));

jest.mock("../../model/search-store", () => ({
    useSearchStore: (selector: (state: unknown) => unknown) =>
        selector({ clearGlobal: mockClearGlobal }),
}));

jest.mock("expo-router", () => ({
    useRouter: () => ({ push: jest.fn() }),
}));

describe("UserSearchResults loading state", () => {
    beforeEach(() => {
        mockUserSearchState = {
            query: "ann",
            results: [],
            isLoading: true,
            error: null,
            hasMore: false,
            pendingUserId: null,
            loadMore: jest.fn(),
            retry: jest.fn(),
            openDirectWithUser: jest.fn(),
        };
    });

    it("renders user result skeleton rows before initial user results arrive", () => {
        render(<UserSearchResults />);

        expect(screen.getAllByTestId("search-result-skeleton")).toHaveLength(3);
    });
});
