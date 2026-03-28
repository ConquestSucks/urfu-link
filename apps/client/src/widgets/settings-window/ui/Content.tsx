import { useUIStore } from "@/shared/model";
import { LinearProgress } from "@/shared/ui";
import React from "react";
import { Text, View } from "react-native";
import { MENU_ITEMS } from "../config/menu";

interface ContentProps {
  activeTab: string;
}

const Content = ({ activeTab }: ContentProps) => {
  const activeItem = MENU_ITEMS.find((item) => item.key === activeTab);

  const { isPending } = useUIStore();

  return (
    activeItem && (
      <View className="flex-1 overflow-hidden h-full">
        <LinearProgress isVisible={isPending} />

        <View className="flex-1 px-8 pb-8 pt-7 gap-6">
          <View className="gap-2">
            <Text className="text-2xl text-white font-bold">
              {activeItem.label}
            </Text>
            <Text className="text-sm text-[#90A1B9]">
              {activeItem.description}
            </Text>
          </View>

          <activeItem.component />
        </View>
      </View>
    )
  );
};

export default Content;
