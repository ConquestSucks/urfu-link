import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import React from "react";
import { View } from "react-native";
import { InboxSidebarListProps, TabType } from "../model/components";
import { Header } from "./Header";
import { List } from "./List";
interface InboxSidebarProps<T> extends InboxSidebarListProps<T> {
    title: string;
    activeTab: TabType;
    onTabChange: (tab: TabType) => void;
    isLoading?: boolean;
    variant?: "chats" | "subjects";
}
export const InboxSidebar = <T,>({ title, data, renderItem, activeTab, onTabChange, isLoading, variant = "chats", }: InboxSidebarProps<T>) => (<View className="bg-app-panel w-[calc(384/1359*100vw)] h-full gap-4 border-r border-white/5">
    <Header title={title} activeTab={activeTab} onTabChange={onTabChange}/>

    {isLoading ? (<View className="px-3 gap-2 overflow-hidden">
        {[...Array(variant === "subjects" ? 3 : 7)].map((_, index) => {
            if (activeTab === "notifications") {
                return <InboxNotificationSkeleton key={index}/>;
            }
            return variant === "subjects" ? (<InboxSubjectSkeleton key={index}/>) : (<InboxChatSkeleton key={index}/>);
        })}
      </View>) : (<List data={data} renderItem={renderItem}/>)}
  </View>);
