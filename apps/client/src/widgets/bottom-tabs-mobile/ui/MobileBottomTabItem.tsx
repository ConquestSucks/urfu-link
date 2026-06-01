import { TabItemProps } from "../model/types";
import React from "react";
import { Pressable, Text, View } from "react-native";
export const MobileBottomTabItem = ({ icon: Icon, label, isActive, onPress, }: TabItemProps) => {
    return (<Pressable onPress={onPress} className="flex-1 items-center justify-center gap-1">
      <View
        className={`h-8 w-12 rounded-full items-center justify-center ${isActive ? "bg-brand-650/12 text-brand-650" : "text-text-disabled"}`}
        testID="mobile-bottom-tab-icon-pill"
      >
        <Icon size={24} className={isActive ? "text-brand-650" : "text-text-subtle"} weight={isActive ? "fill" : "regular"}/>
      </View>
      <Text className={`text-xs font-medium ${isActive ? "text-brand-600" : "text-text-subtle"}`}>
        {label}
      </Text>
    </Pressable>);
};
