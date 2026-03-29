import { ChatView } from "@/widgets/chat-window";
import { useLocalSearchParams } from "expo-router";
export default function ChatScreen() {
    const { id } = useLocalSearchParams<{
        id: string;
    }>();
    return <ChatView chatId={id} type="chat"/>;
}
