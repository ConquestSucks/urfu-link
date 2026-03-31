import { useWindowSize } from "@/shared/lib/useWindowSize";
import { BellIcon, ChatCircleTextIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, View } from "react-native";
import { ViewType } from "../model/types";

interface ViewToggleProps {
    currentView: ViewType;
    onCurrentViewChange: (view: ViewType) => void;
}

const TOGGLE_ITEMS = [
    { id: "messages" as const, icon: ChatCircleTextIcon, label: "Личные чаты" },
    { id: "notifications" as const, icon: BellIcon, label: "Уведомления", hasBadge: true },
];

export const ViewToggle = ({ currentView, onCurrentViewChange }: ViewToggleProps) => {
    const { isMobile } = useWindowSize();

    const containerClasses = isMobile
        ? "flex-row items-center gap-1"
        : "bg-white/5 border gap-1 border-white/5 rounded-2xl flex-row w-fit p-[5px]";

    return (
        <View className={containerClasses}>
            {TOGGLE_ITEMS.map(({ id, icon: Icon, label, hasBadge }) => {
                const isActive = currentView === id;

                const buttonClasses = isMobile
                    ? "p-2.5"
                    : `p-2 rounded-xl transition-colors duration-300 ${
                          isActive ? "bg-brand-600 shadow-brand-soft" : "hover:bg-white/5"
                      }`;

                const iconSize = isMobile ? 24 : 18;
                const iconWeight = isMobile && isActive ? "fill" : "regular";

                const iconColor = isActive
                    ? "text-white"
                    : isMobile
                      ? "text-text-subtle"
                      : "text-text-placeholder";

                return (
                    <Pressable
                        key={id}
                        onPress={() => onCurrentViewChange(id)}
                        hitSlop={isMobile ? 8 : 0}
                        accessibilityRole="button"
                        accessibilityLabel={label}
                        accessibilityState={{ selected: isActive }}
                        className={buttonClasses}
                    >
                        <View className="relative">
                            <Icon size={iconSize} weight={iconWeight} className={iconColor} />

                            {hasBadge && (
                                <View className="absolute top-0 right-0 w-2 h-2 bg-red-500 rounded-full border border-app-bg" />
                            )}
                        </View>
                    </Pressable>
                );
            })}
        </View>
    );
};
