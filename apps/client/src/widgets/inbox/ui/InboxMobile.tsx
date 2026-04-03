import React from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import { SearchBar } from "@/shared/ui";
import { MobileHeader } from "@/widgets/header-mobile";
import { InboxTabsMobile } from "./InboxTabsMobile";
import { List } from "./List";
import { InboxListProps } from "../model/components";

interface InboxMobileProps<T> extends InboxListProps<T> {
    data: T[];
    renderItem: (item: T) => React.ReactNode;
    isLoading?: boolean;
}

export const InboxMobile = <T,>({ data, renderItem, isLoading }: InboxMobileProps<T>) => {
    const { currentTab, currentView, createTabHref, createViewHref } = useInboxRouting();

    return (
        <View className="flex-1 bg-app-bg">
            <MobileHeader 
                currentView={currentView} 
                createHref={createViewHref} 
            />

            <View className="px-4 py-2">
                <SearchBar placeholder="Поиск" />
            </View>

            <View className="px-4 py-2">
                <InboxTabsMobile 
                    currentTab={currentTab} 
                    createHref={createTabHref} 
                />
            </View>

            <View className="flex-1">
                {isLoading ? (
                    <View className="gap-2 overflow-hidden px-3">
                        {[...Array(7)].map((_, index) => (
                            currentView === "notifications" 
                                ? <InboxNotificationSkeleton key={index} />
                                : currentTab === "subjects" 
                                    ? <InboxSubjectSkeleton key={index} />
                                    : <InboxChatSkeleton key={index} />
                        ))}
                    </View>
                ) : (
                    <List key={`${currentTab}-${currentView}`} data={data} renderItem={renderItem} />
                )}
            </View>
        </View>
    );
};