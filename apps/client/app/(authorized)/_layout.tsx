import { GlobalSidebar } from "@/widgets/global-sidebar";
import { SettingsWindow } from "@/widgets/settings-window";
import { Slot } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { useState } from "react";
import { View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

export default function AuthLayout() {
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  return (
    <SafeAreaView
      className="flex-1 bg-[#080D1D]"
      edges={["top", "left", "right"]}
    >
      <StatusBar style="light" />
      <View className="flex-1 flex-row">
        <GlobalSidebar onSettingsPress={() => setIsSettingsOpen(true)} />

        <View className="flex-1 bg-[#111827]">
          <Slot />
        </View>
      </View>
      <SettingsWindow
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
      />
    </SafeAreaView>
  );
}
