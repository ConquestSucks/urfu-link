import { render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { ChatHeader } from "../ChatHeader";

const mockCurrentUserId = "11111111-1111-4111-8111-111111111111";
const mockPeerUserId = "22222222-2222-4222-8222-222222222222";
let mockPeerPresence: unknown;
let mockParticipants: Array<{
    userId: string;
    displayName: string;
    avatarUrl: string | null;
}>;

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("@/shared/lib/safeGoBack", () => ({
    safeGoBack: jest.fn(),
}));

jest.mock("@/shared/ui", () => ({
    Avatar: ({ name }: { name: string }) => {
        const { Text } = require("react-native");
        return <Text>{name}</Text>;
    },
    StatusIndicator: ({ status }: { status: string }) => {
        const { View } = require("react-native");
        return <View testID={`status-indicator-${status}`} />;
    },
    Skeleton: ({ testID }: { testID?: string }) => {
        const { View } = require("react-native");
        return <View testID={testID} />;
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    const Icon = ({ children }: { children?: ReactNode }) => <Text>{children}</Text>;

    return {
        CaretLeftIcon: Icon,
    };
});

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: unknown) => unknown) =>
        selector({
            conversations: [
                {
                    id: "chat-1",
                    type: "Direct",
                    title: null,
                    lastMessagePreview: "hello",
                },
            ],
        }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => mockParticipants,
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => mockCurrentUserId,
}));

jest.mock("@/entities/presence", () => ({
    LastSeenLabel: ({ lastSeenAt }: { lastSeenAt: string }) => {
        const { Text } = require("react-native");
        return <Text>{`last seen ${lastSeenAt}`}</Text>;
    },
    TypingIndicator: () => {
        const { Text } = require("react-native");
        return <Text>Печатает...</Text>;
    },
    presenceStatusToLabel: (status: string) => {
        if (status === "Online") return "В сети";
        if (status === "Offline") return "Не в сети";
        return status;
    },
    useConversationTypers: () => [],
    usePresenceStore: (selector: (state: unknown) => unknown) =>
        selector({
            watchUserPresence: jest.fn(),
            unwatchUserPresence: jest.fn(),
        }),
    useUserPresence: () => mockPeerPresence,
}));

jest.mock("../ChatHeaderActions", () => ({
    ChatHeaderActions: () => {
        const { Text } = require("react-native");
        return <Text>actions</Text>;
    },
}));

jest.mock("../UserProfileModal", () => ({
    UserProfileModal: () => null,
}));

describe("ChatHeader presence state", () => {
    beforeEach(() => {
        mockPeerPresence = undefined;
        mockParticipants = [
            {
                userId: mockCurrentUserId,
                displayName: "Current User",
                avatarUrl: null,
            },
            {
                userId: mockPeerUserId,
                displayName: "Peer User",
                avatarUrl: null,
            },
        ];
    });

    it("shows identity skeletons while direct chat participants are loading", () => {
        mockParticipants = [];

        render(<ChatHeader chatId="chat-1" />);

        expect(screen.getByTestId("chat-header-avatar-skeleton")).toBeTruthy();
        expect(screen.getByTestId("chat-header-title-skeleton")).toBeTruthy();
        expect(screen.queryByText("Ð›Ð¸Ñ‡Ð½Ñ‹Ð¹ Ñ‡Ð°Ñ‚")).toBeNull();
    });

    it("shows a status skeleton before the first presence snapshot", () => {
        render(<ChatHeader chatId="chat-1" />);

        expect(screen.getByTestId("chat-header-presence-skeleton")).toBeTruthy();
        expect(screen.queryByText("Не в сети")).toBeNull();
    });

    it("shows offline only after an offline presence snapshot", () => {
        mockPeerPresence = {
            userId: mockPeerUserId,
            status: "Offline",
            platforms: [],
            lastSeenAt: null,
        };

        render(<ChatHeader chatId="chat-1" />);

        expect(screen.queryByTestId("chat-header-presence-skeleton")).toBeNull();
        expect(screen.getByText("Не в сети")).toBeTruthy();
    });
});
