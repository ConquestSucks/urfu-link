import React from "react";
import { Pressable, Text, View } from "react-native";
import Animated from "react-native-reanimated";
import { AnimatedViewStyle } from "../types";
interface SidebarItemProps {
    icon: React.ComponentType<any>;
    label: string;
    isActive: boolean;
    textAnimatedStyle?: AnimatedViewStyle;
    onPress?: () => void;
}
export const SidebarItem = ({ icon: Icon, label, isActive, textAnimatedStyle, onPress, }: SidebarItemProps) => {
    return (<Pressable className={`flex-row gap-3 items-center px-[17.5] py-[14px] rounded-xl transition-colors duration-300 select-none ${isActive
            ? "bg-brand-600 shadow-brand-soft"
            : "bg-transparent hover:bg-white/5"}`} onPress={onPress}>
      <View>
        <Icon size={20} className={isActive ? "text-white" : "text-text-muted"}/>
      </View>
      <Animated.View style={textAnimatedStyle ?? {}}>
        <Text numberOfLines={1} className={`text-[15px] font-medium ${isActive ? "text-white" : "text-text-muted"}`}>
          {label}
        </Text>
      </Animated.View>
    </Pressable>);
};
