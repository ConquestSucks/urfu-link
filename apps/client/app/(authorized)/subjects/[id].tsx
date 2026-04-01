import { ChatView } from "@/widgets/chat";
import { useLocalSearchParams } from "expo-router";
export default function SubjectScreen() {
    const { id } = useLocalSearchParams<{
        id: string;
    }>();
    return <ChatView chatId={id} type="subject" />;
}
