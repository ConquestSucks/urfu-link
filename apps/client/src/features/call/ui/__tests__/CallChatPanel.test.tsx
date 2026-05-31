import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";

jest.mock("@/shared/lib/config", () => ({
    appConfig: {
        appEnv: "dev",
        apiUrl: "http://localhost:5080",
        keycloakUrl: "http://localhost:8080",
    },
}));

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(),
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {},
    },
}));

jest.mock("@/shared/lib/current-user", () => ({
    resolveCurrentUserId: jest.fn(async () => "user-1"),
}));

jest.mock("@/shared/lib/message-sounds", () => ({
    playMessageSound: jest.fn(),
}));

jest.mock("@/shared/lib/browser-notifications", () => ({
    showMessageBrowserNotification: jest.fn(),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useAuthStore: {
        getState: () => ({ userId: "user-1" }),
    },
}));

jest.mock("@/widgets/chat/lib/use-conversation-send", () => ({
    useConversationSend: (conversationId: string) => {
        const { useChatStore } = require("@/entities/conversation/model/chat-store");
        return async (text: string, files: unknown[]) =>
            useChatStore
                .getState()
                .sendMessage(conversationId, text, files, undefined, undefined);
    },
}));

jest.mock("@/widgets/chat/ui/MessagesList", () => ({
    MessagesList: ({ chatId, type }: { chatId: string; type: string }) => {
        const { Text } = require("react-native");
        return <Text testID="call-chat-messages">{`${type}:${chatId}`}</Text>;
    },
}));

jest.mock("@/widgets/chat/ui/chat-input/Input", () => ({
    ChatInput: ({
        conversationId,
        onSend,
    }: {
        conversationId: string;
        onSend: (text: string, files: unknown[]) => Promise<void>;
    }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="call-chat-input" onPress={() => onSend("hello", [])}>
                <Text>{conversationId}</Text>
            </Pressable>
        );
    },
}));

const { CallChatPanel } = require("../CallChatPanel");
const { useChatStore } = require("@/entities/conversation/model/chat-store");

describe("CallChatPanel", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("renders the existing conversation messages and composer", async () => {
        const sendMessage = jest.fn(async () => undefined);
        useChatStore.setState({ sendMessage });

        render(<CallChatPanel conversationId="conversation-1" onClose={jest.fn()} />);

        expect(screen.getByTestId("call-chat-messages").props.children).toBe(
            "chat:conversation-1",
        );

        fireEvent.press(screen.getByTestId("call-chat-input"));

        await waitFor(() => {
            expect(sendMessage).toHaveBeenCalledWith(
                "conversation-1",
                "hello",
                [],
                undefined,
                undefined,
            );
        });
    });
});
