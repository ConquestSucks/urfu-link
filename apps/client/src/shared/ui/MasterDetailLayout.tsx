import { Slot, Stack } from "expo-router";
import React from "react";
import { Platform, View } from "react-native";

interface MasterDetailLayoutProps {
  sidebar: React.ReactNode;
}

export const MasterDetailLayout = ({ sidebar }: MasterDetailLayoutProps) => {
  const isWeb = Platform.OS === "web";

  return (
    <View className="flex-1 flex-row">
      {sidebar}

      <View className="flex-1">
        {isWeb ? (
          <Slot />
        ) : (
          <Stack screenOptions={{ headerShown: false }}>
            <Stack.Screen name="index" />
            <Stack.Screen name="[id]" />
          </Stack>
        )}
      </View>
    </View>
  );
};
