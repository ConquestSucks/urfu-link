import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import React from "react";
import { View } from "react-native";
import { InboxListProps } from "../model/components";
import { Header } from "./Header";
import { List } from "./List";
import { useTabState, useViewState } from "@/store/useInboxStore";
import { TabType } from "@/entities/tab";
import { ViewType } from "@/entities/view";
interface InboxProps<T> extends InboxListProps<T> {
    data: T[];
    renderItem: (item: T) => React.ReactNode;
    isLoading?: boolean;
    currentTab: TabType;
    currentView: ViewType;
    onCurrentViewChange: (view: ViewType) => void;
}
export const Inbox = <T,>({
    data,
    renderItem,
    isLoading,
    currentTab,
    currentView,
    onCurrentViewChange,
}: InboxProps<T>) => {
    const title = currentTab === "chats" ? "Личные чаты" : "Уведомления";
    return (
        <View className="bg-app-panel w-[calc(384/1359*100vw)] h-full gap-4 border-r border-white/5">
            <Header
                title={title}
                currentView={currentView}
                onCurrentViewChange={onCurrentViewChange}
            />

            {isLoading ? (
                <View className="px-3 gap-2 overflow-hidden">
                    {[...Array(currentTab === "subjects" ? 3 : 7)].map((_, index) => {
                        if (currentView === "notifications") {
                            return <InboxNotificationSkeleton key={index} />;
                        }
                        return currentTab === "subjects" ? (
                            <InboxSubjectSkeleton key={index} />
                        ) : (
                            <InboxChatSkeleton key={index} />
                        );
                    })}
                </View>
            ) : (
                <List key={currentTab} data={data} renderItem={renderItem} />
            )}
        </View>
    );
};
