import { render, screen } from "@testing-library/react-native";
import { ManageNotifications } from "../ManageNotifications";

const mockMutate = jest.fn();

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
    getBrowserNotificationPermission: () => "unsupported",
    requestBrowserNotificationPermission: jest.fn(),
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
            <Pressable disabled={disabled} onPress={() => onValueChange(!value)}>
                <Text>{label}</Text>
                <Text>{description}</Text>
            </Pressable>
        ),
    };
});

describe("ManageNotifications", () => {
    beforeEach(() => {
        mockMutate.mockClear();
    });

    it("does not render the removed direct message toggle", () => {
        render(<ManageNotifications />);

        expect(screen.queryByText("Новые сообщения")).toBeNull();
        expect(screen.getByText("Звук уведомлений")).toBeTruthy();
        expect(screen.getByText("Сообщения в чатах дисциплин")).toBeTruthy();
        expect(screen.getByText("Упоминания")).toBeTruthy();
    });
});
