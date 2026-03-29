import { InboxChat } from "@/entities/inbox-chat";
import { Text, View } from "react-native";
import type { InboxSubjectProps } from "../model/types";

type InboxSubjectGroupProps = {
    subject: InboxSubjectProps;
    activeChatId?: string;
    onChatPress: (chatId: string) => void;
};

export function InboxSubjectGroup({ subject, activeChatId, onChatPress }: InboxSubjectGroupProps) {
    return (
        <View>
            <Text className="px-4 py-3 text-[11px] leading-1 font-bold text-[#8B8FA8] uppercase tracking-[0.1em] opacity-80">
                {subject.title}
            </Text>
            <View className="md:gap-1">
                {subject.messages.map((chat) => (
                    <InboxChat
                        key={chat.id}
                        {...chat}
                        isActive={chat.id === activeChatId}
                        onPress={() => onChatPress(chat.id)}
                    />
                ))}
            </View>
        </View>
    );
}
