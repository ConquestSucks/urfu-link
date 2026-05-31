import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { Platform } from "react-native";
import { ManageNotifications } from "../ManageNotifications";

const mockMutate = jest.fn();
const mockRequestBrowserNotificationPermission = jest.fn();
const mockSetBrowserNotificationsEnabled = jest.fn();
let mockBrowserPermission: NotificationPermission | "unsupported" = "unsupported";
let mockBrowserNotificationsEnabled = true;
const originalPlatformOS = Platform.OS;

jest.mock("@/entities/user", () => ({
    useCurrentUser: () => ({
        data: {
            notifications: {
                newMessages: false,
                notificationSound: true,
                disciplineChatMessages: true,
                mentions: true,
                mutedConversationIds: [],
            },
        },
        isLoading: false,
    }),
    useUpdateNotifications: () => ({
        mutate: mockMutate,
        isPending: false,
    }),
}));

jest.mock("@/shared/lib/browser-notifications", () => ({
    getBrowserNotificationPermission: () => mockBrowserPermission,
    getBrowserNotificationsEnabled: () => mockBrowserNotificationsEnabled,
    requestBrowserNotificationPermission: () => mockRequestBrowserNotificationPermission(),
    setBrowserNotificationsEnabled: (enabled: boolean) =>
        mockSetBrowserNotificationsEnabled(enabled),
}));

jest.mock("@/shared/ui", () => {
    const { Text, View, Pressable } = require("react-native");
    return {
        Skeleton: ({ testID }: { testID?: string }) => <View testID={testID} />,
        SwitchCardSkeleton: () => <View testID="switch-card-skeleton" />,
        SwitchCard: ({
            label,
            description,
            value,
            onValueChange,
            disabled,
        }: {
            label: string;
            description: string;
            value: boolean;
            onValueChange: (value: boolean) => void;
            disabled?: boolean;
        }) => (
            <Pressable
                testID={`switch-card-${label}`}
                disabled={disabled}
                onPress={() => onValueChange(!value)}
            >
                <Text>{label}</Text>
                <Text>{description}</Text>
            </Pressable>
        ),
    };
});

describe("ManageNotifications", () => {
    beforeEach(() => {
        mockMutate.mockClear();
        mockRequestBrowserNotificationPermission.mockReset();
        mockSetBrowserNotificationsEnabled.mockClear();
        mockBrowserPermission = "unsupported";
        mockBrowserNotificationsEnabled = true;
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
    });

    afterAll(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
    });

    it("does not render the removed direct message toggle", () => {
        render(<ManageNotifications />);

        expect(screen.queryByText("Новые сообщения")).toBeNull();
        expect(screen.getByText("Звук уведомлений")).toBeTruthy();
        expect(screen.getByText("Сообщения в чатах дисциплин")).toBeTruthy();
        expect(screen.getByText("Упоминания")).toBeTruthy();
    });

    it("keeps the browser notification toggle visible after permission is granted", async () => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        mockBrowserPermission = "granted";
        mockBrowserNotificationsEnabled = true;

        render(<ManageNotifications />);

        expect(await screen.findByText("Браузерные уведомления")).toBeTruthy();

        fireEvent.press(screen.getByTestId("switch-card-Браузерные уведомления"));

        expect(mockSetBrowserNotificationsEnabled).toHaveBeenCalledWith(false);
    });

    it("requests browser permission before enabling browser notifications", async () => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        mockBrowserPermission = "default";
        mockBrowserNotificationsEnabled = true;
        mockRequestBrowserNotificationPermission.mockResolvedValue("granted");

        render(<ManageNotifications />);

        fireEvent.press(await screen.findByTestId("switch-card-Браузерные уведомления"));

        await waitFor(() => {
            expect(mockRequestBrowserNotificationPermission).toHaveBeenCalledTimes(1);
        });
        expect(mockSetBrowserNotificationsEnabled).toHaveBeenCalledWith(true);
    });
});
