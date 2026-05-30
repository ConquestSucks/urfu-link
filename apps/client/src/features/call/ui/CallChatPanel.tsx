import { View, Pressable, Text } from "react-native";
import { ChatInput } from "@/widgets/chat/ui/chat-input/Input";
import { MessagesList } from "@/widgets/chat/ui/MessagesList";
import { useConversationSend } from "@/widgets/chat/lib/use-conversation-send";
import { XIcon } from "@/shared/ui/phosphor";

type CallChatPanelProps = {
    conversationId: string;
    type?: "chat" | "subject";
    onClose: () => void;
};

export const CallChatPanel = ({
    conversationId,
    type = "chat",
    onClose,
}: CallChatPanelProps) => {
    const handleSend = useConversationSend(conversationId);

    return (
        <View className="h-full w-full bg-app-card">
            <View className="h-12 px-4 border-b border-white/10 flex-row items-center justify-between">
                <Text className="text-white font-semibold">Чат</Text>
                <Pressable
                    testID="call-chat-close"
                    className="h-8 w-8 rounded-full items-center justify-center bg-white/10"
                    onPress={onClose}
                >
                    <XIcon size={14} className="text-white" />
                </Pressable>
            </View>
            <MessagesList chatId={conversationId} type={type} />
            <ChatInput conversationId={conversationId} onSend={handleSend} />
        </View>
    );
};
