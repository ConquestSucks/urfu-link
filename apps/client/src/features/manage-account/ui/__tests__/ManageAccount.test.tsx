import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import type { ReactNode } from "react";
import { Platform } from "react-native";

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
    const originalPlatformOS = Platform.OS;
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        jest.clearAllMocks();
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => originalPlatformOS,
        });
        globalThis.fetch = originalFetch;
        mockCurrentUserState = {
            data: {
                account: { avatarUrl: null },
                identity: { name: "Ivan Petrov" },
            },
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

    it("uploads a native image picker asset without converting it to a browser File", async () => {
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => "android",
        });
        globalThis.fetch = jest.fn();
        const ImagePicker = require("expo-image-picker");
        ImagePicker.requestMediaLibraryPermissionsAsync.mockResolvedValue({ granted: true });
        ImagePicker.launchImageLibraryAsync.mockResolvedValue({
            canceled: false,
            assets: [
                {
                    fileName: "profile.webp",
                    mimeType: "image/webp",
                    uri: "file:///avatar/profile.webp",
                },
            ],
        });

        render(<ManageAccount />);

        fireEvent.press(screen.getByText("Изменить"));

        await waitFor(() => {
            expect(mockUploadAvatar.mutate).toHaveBeenCalledWith({
                name: "profile.webp",
                type: "image/webp",
                uri: "file:///avatar/profile.webp",
            });
        });
        expect(globalThis.fetch).not.toHaveBeenCalled();
    });
});
