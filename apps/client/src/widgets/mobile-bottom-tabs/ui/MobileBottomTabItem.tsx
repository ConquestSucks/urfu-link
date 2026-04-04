import { TabItemProps } from "../model/types";
import React from "react";
import { Pressable, Text, View } from "react-native";
export const MobileBottomTabItem = ({ icon: Icon, label, isActive, onPress, }: TabItemProps) => {
    return (<Pressable onPress={onPress} className="flex-1 items-center justify-center gap-1">
      <View className={`items-center justify-center ${isActive ? "text-[#2563EB]" : "text-[#45556C]"}`}>
        <Icon size={24} color={isActive ? "#2563EB" : "#8B8FA8"} weight={isActive ? "fill" : "regular"}/>
      </View>
      <Text className={`text-xs font-medium ${isActive ? "text-[#2B7FFF]" : "text-[#8B8FA8]"}`}>
        {label}
      </Text>
    </Pressable>);
};
