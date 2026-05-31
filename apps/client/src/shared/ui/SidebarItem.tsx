import { AnimatedView } from "@/shared/lib/nativewind-interop";
import React from "react";
import { Pressable, Text, View } from "react-native";
import { AnimatedViewStyle } from "../types";
import { glow } from "./glow";
interface SidebarItemProps {
    icon: React.ComponentType<any>;
    label: string;
    isActive: boolean;
    textAnimatedStyle?: AnimatedViewStyle;
    onPress?: () => void;
    badgeCount?: number;
}
export const SidebarItem = ({
    icon: Icon,
    label,
    isActive,
    textAnimatedStyle,
    onPress,
    badgeCount = 0,
}: SidebarItemProps) => {
    return (
        <Pressable
            style={isActive ? glow.brandSoft : undefined}
            className={`overflow-hidden flex-row gap-3 items-center px-[17.5] py-[14px] rounded-xl transition-colors duration-300 select-none ${
                isActive ? "bg-brand-650" : "bg-transparent hover:bg-white/5"
            }`}
            onPress={onPress}
        >
            <View>
                <Icon size={20} className={isActive ? "text-white" : "text-text-muted"} />
                {badgeCount > 0 ? (
                    <View
                        className="absolute -top-2 -right-2 min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 items-center justify-center border border-app-bg"
                        style={glow.badge}
                    >
                        <Text className="text-[10px] leading-none text-white font-bold">
                            {badgeCount > 99 ? "99+" : badgeCount}
                        </Text>
                    </View>
                ) : null}
            </View>
            <AnimatedView style={[{ flex: 1 }, textAnimatedStyle ?? {}]}>
                <Text
                    numberOfLines={1}
                    className={`text-[15px] font-medium ${isActive ? "text-white" : "text-text-muted"}`}
                >
                    {label}
                </Text>
            </AnimatedView>
        </Pressable>
    );
};
