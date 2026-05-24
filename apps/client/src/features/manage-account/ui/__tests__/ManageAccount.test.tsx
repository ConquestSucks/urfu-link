import { render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { ManageAccount } from "../ManageAccount";

const mockUploadAvatar = {
    isPending: false,
    mutate: jest.fn(),
};
const mockDeleteAvatar = {
    isPending: false,
    mutate: jest.fn(),
};
let mockCurrentUserState: unknown;

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

jest.mock("@/entities/user", () => ({
    useCurrentUser: () => mockCurrentUserState,
    useUploadAvatar: () => mockUploadAvatar,
    useDeleteAvatar: () => mockDeleteAvatar,
}));

jest.mock("expo-image-picker", () => ({
    requestMediaLibraryPermissionsAsync: jest.fn(),
    launchImageLibraryAsync: jest.fn(),
}));

describe("ManageAccount", () => {
    beforeEach(() => {
        mockCurrentUserState = {
            data: undefined,
            isLoading: false,
        };
    });

    it("keeps the account form shape visible while the profile is loading", () => {
        mockCurrentUserState = {
            data: undefined,
            isLoading: true,
        };

        render(<ManageAccount />);

        expect(screen.getByTestId("account-avatar-skeleton")).toBeTruthy();
        expect(screen.getAllByTestId("account-input-skeleton")).toHaveLength(2);
        expect(screen.queryByTestId("account-loading-indicator")).toBeNull();
    });
});
