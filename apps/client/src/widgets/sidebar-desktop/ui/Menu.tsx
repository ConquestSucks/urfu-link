import { AnimatedViewStyle } from "@/shared/types";
import { SidebarItem } from "@/shared/ui/SidebarItem";
import { Link, usePathname } from "expo-router";
import { useEffect } from "react";
import { MENU_ITEMS } from "../config/menu";
import {
    getConversationUnreadCount,
    useChatStore,
} from "@/entities/conversation/model/chat-store";
import { getNotificationScopeUnreadCount, useNotificationBadge } from "@/features/notifications";
import { useNotificationStore } from "@/shared/store/notification-store";

export const Menu = ({ textAnimatedStyle, }: {
    textAnimatedStyle: AnimatedViewStyle;
}) => {
    const pathname = usePathname();
    const conversations = useChatStore((s) => s.conversations);
    const unreadByConversation = useChatStore((s) => s.unreadByConversation);
    const loadConversations = useChatStore((s) => s.loadConversations);
    const { data } = useNotificationBadge();
    const liveBadge = useNotificationStore((s) => s.badge);
    const badge = liveBadge ?? data;

    useEffect(() => {
        void loadConversations("Direct");
        void loadConversations("Discipline");
    }, [loadConversations]);

    const chatUnread = conversations
        .filter((conversation) => conversation.type === "Direct")
        .reduce(
            (sum, conversation) =>
                sum + getConversationUnreadCount(conversation, unreadByConversation),
            0,
        );
    const disciplineUnread = conversations
        .filter((conversation) => conversation.groupSubtype === "Discipline")
        .reduce(
            (sum, conversation) =>
                sum + getConversationUnreadCount(conversation, unreadByConversation),
            0,
        );
    const countsByHref: Record<string, number> = {
        "/chats": chatUnread + getNotificationScopeUnreadCount(badge, "chats"),
        "/subjects": disciplineUnread + getNotificationScopeUnreadCount(badge, "disciplines"),
    };

    return (<>
      {MENU_ITEMS.map((item) => {
            const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
            return (<Link key={item.href.toString()} href={item.href} asChild>
            <SidebarItem
                icon={item.icon}
                label={item.label}
                isActive={isActive}
                textAnimatedStyle={textAnimatedStyle}
                badgeCount={countsByHref[item.href.toString()] ?? 0}
            />
          </Link>);
        })}
    </>);
};
