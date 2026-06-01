import { useWindowSize } from "@/shared/lib/useWindowSize";
import {
    getNotificationScopeUnreadCount,
    type NotificationScope,
    useNotificationBadge,
} from "@/features/notifications";
import { useNotificationStore } from "@/shared/store/notification-store";
import { BellIcon, ChatCircleTextIcon } from "@/shared/ui/phosphor";
import { Link } from "expo-router";
import React from "react";
import { Pressable, View } from "react-native";
import { ViewType } from "../model/types";
import { glow } from "@/shared/ui/glow";

interface ViewToggleProps {
    currentView: ViewType;
    createHref: (view: ViewType) => any;
    notificationScope?: NotificationScope;
}

const TOGGLE_ITEMS = [
    { id: "messages" as const, icon: ChatCircleTextIcon, label: "Личные чаты" },
    { id: "notifications" as const, icon: BellIcon, label: "Уведомления", hasBadge: true },
];

export const ViewToggle = ({
    currentView,
    createHref,
    notificationScope = "chats",
}: ViewToggleProps) => {
    const { isMobile } = useWindowSize();
    const { data } = useNotificationBadge();
    const liveBadge = useNotificationStore((s) => s.badge);
    const unreadCount = getNotificationScopeUnreadCount(
        liveBadge ?? data,
        notificationScope,
    );

    const containerClasses = isMobile
        ? "flex-row items-center gap-1"
        : "bg-white/5 border gap-1 border-white/5 rounded-2xl flex-row w-fit p-[5px]";

    return (
        <View className={containerClasses}>
            {TOGGLE_ITEMS.map(({ id, icon: Icon, label, hasBadge }) => {
                const isActive = currentView === id;

                const buttonClasses = isMobile
                    ? `p-2.5 rounded-full ${isActive ? "bg-brand-650/12" : "active:bg-white/5"}`
                    : `p-2 rounded-xl transition-colors duration-300 ${isActive ? "bg-brand-650" : "hover:bg-white/5"}`;
                const iconSize = isMobile ? 24 : 18;
                const iconWeight = isMobile && isActive ? "fill" : "regular";
                const iconColor = isActive
                    ? isMobile
                      ? "text-brand-500"
                      : "text-white"
                    : isMobile
                      ? "text-text-subtle"
                      : "text-text-placeholder";

                return (
                    <Link key={id} href={createHref(id)} replace asChild>
                        <Pressable
                            hitSlop={isMobile ? 8 : 0}
                            accessibilityRole="tab"
                            accessibilityLabel={label}
                            accessibilityState={{ selected: isActive }}
                            className={buttonClasses}
                            testID={`view-toggle-${id}`}
                            style={!isMobile && isActive ? glow.brandSoft : undefined}
                        >
                            <View className="relative">
                                <Icon size={iconSize} weight={iconWeight} className={iconColor} />
                                {hasBadge && unreadCount > 0 && (
                                    <View className="absolute top-0 right-0 w-2 h-2 bg-red-500 rounded-full border border-app-bg" style={glow.badge} />
                                )}
                            </View>
                        </Pressable>
                    </Link>
                );
            })}
        </View>
    );
};
