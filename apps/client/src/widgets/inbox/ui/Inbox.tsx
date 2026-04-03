import React from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import { Header } from "./Header";
import { List } from "./List";
import { InboxListProps } from "../model/components";

interface InboxProps<T> extends InboxListProps<T> {
    data: T[];
    renderItem: (item: T) => React.ReactNode;
    isLoading?: boolean;
}

export const Inbox = <T,>({ data, renderItem, isLoading }: InboxProps<T>) => {
    const { currentTab, currentView } = useInboxRouting();

    const title = currentTab === "chats" ? "Личные чаты" : "Предметы";

    return (
        <View className="bg-app-panel w-[calc(384/1359*100vw)] h-full gap-4 border-r border-white/5">
            <Header title={title} />

            {isLoading ? (
                <View className="px-3 gap-2 overflow-hidden">
                    {[...Array(currentTab === "subjects" ? 3 : 7)].map((_, index) =>
                        currentView === "notifications" ? (
                            <InboxNotificationSkeleton key={index} />
                        ) : currentTab === "subjects" ? (
                            <InboxSubjectSkeleton key={index} />
                        ) : (
                            <InboxChatSkeleton key={index} />
                        ),
                    )}
                </View>
            ) : (
                <List key={`${currentTab}-${currentView}`} data={data} renderItem={renderItem} />
            )}
        </View>
    );
};
