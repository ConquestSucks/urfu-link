import { ChatView } from "@/widgets/chat";
import { useHideMobileBottomTabsOnFocus } from "@/widgets/bottom-tabs-mobile/model/visibility";

export default function ChatScreen() {
    useHideMobileBottomTabsOnFocus();

    return <ChatView />;
}
