import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import { SearchBar } from "@/shared/ui";
import { MobileHeader } from "@/widgets/mobile-header";
import React from "react";
import { View } from "react-native";
import { InboxSidebarListProps, TabType } from "../model/components";
import { InboxTabsMobile } from "./InboxTabsMobile";
import { List } from "./List";
interface InboxSidebarMobileProps<T> extends InboxSidebarListProps<T> {
  activeTab: TabType;
  onTabChange: (tab: TabType) => void;
  isLoading?: boolean;
  listMode?: "messages" | "notifications";
  onListModeChange?: (mode: "messages" | "notifications") => void;
}
export const InboxSidebarMobile = <T,>({ data, renderItem, activeTab, onTabChange, isLoading, listMode = "messages", onListModeChange, }: InboxSidebarMobileProps<T>) => (<View className="flex-1 bg-app-bg">
  <MobileHeader inboxListMode={listMode} onInboxListModeChange={onListModeChange} />

  <View className="px-4 py-2">
    <SearchBar placeholder="Поиск" />
  </View>

  <View className="px-4 py-2">
    <InboxTabsMobile activeTab={activeTab} onTabChange={onTabChange} />
  </View>

  <View className="flex-1">
    {isLoading ? (<View className="gap-2 overflow-hidden">
      {[...Array(7)].map((_, index) => {
        if (listMode === "notifications") {
          return <InboxNotificationSkeleton key={index} />;
        }
        return activeTab === "subjects" ? (<InboxSubjectSkeleton key={index} />) : (<InboxChatSkeleton key={index} />);
      })}
    </View>) : (<List data={data} renderItem={renderItem} />)}
  </View>
</View>);
