import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import { SearchBar } from "@/shared/ui";
import { MobileHeader } from "@/widgets/header-mobile";
import React from "react";
import { View } from "react-native";
import { InboxListProps } from "../model/components";
import { InboxTabsMobile } from "./InboxTabsMobile";
import { List } from "./List";
import { TabType } from "@/entities/tab";
import { ViewType } from "@/entities/view";
interface InboxMobileProps<T> extends InboxListProps<T> {
    currentTab: TabType;
    onTabChange: (tab: TabType) => void;
    isLoading?: boolean;
    currentView?: ViewType;
    onCurrentViewChange: (mode: ViewType) => void;
}
export const InboxMobile = <T,>({
    data,
    renderItem,
    currentTab,
    onTabChange,
    isLoading,
    currentView = "messages",
    onCurrentViewChange,
}: InboxMobileProps<T>) => (
    <View className="flex-1 bg-app-bg">
        <MobileHeader currentView={currentView} onCurrentViewChange={onCurrentViewChange} />

        <View className="px-4 py-2">
            <SearchBar placeholder="Поиск" />
        </View>

        <View className="px-4 py-2">
            <InboxTabsMobile currentTab={currentTab} onTabChange={onTabChange} />
        </View>

        <View className="flex-1">
            {isLoading ? (
                <View className="gap-2 overflow-hidden">
                    {[...Array(7)].map((_, index) => {
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
                <List data={data} renderItem={renderItem} />
            )}
        </View>
    </View>
);
