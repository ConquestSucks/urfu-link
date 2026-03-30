import { useUserStore } from "@/entities/user";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/mobile-bottom-tabs/config/layout";
import { Avatar, StatusIndicator } from "@/shared/ui";
import { AtIcon, BellIcon, CaretRightIcon, EnvelopeSimpleIcon, MonitorIcon, ShieldCheckIcon, SignOutIcon, SpeakerHighIcon, } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View, ScrollView } from "react-native";
import { router, type Href } from "expo-router";
export const ProfileMobile = () => {
    const { userName, userDescription, email, avatarUrl, userHandle } = useUserStore();
    const menuItems = [
        {
            icon: ShieldCheckIcon,
            label: "Приватность",
            onPress: () => router.push("/profile/privacy")
        },
        { icon: MonitorIcon, label: "Устройства", onPress: () => { } },
        {
            icon: BellIcon,
            label: "Уведомления",
            onPress: () => router.push("/profile/notifications" as Href),
        },
        { icon: SpeakerHighIcon, label: "Звук и видео", onPress: () => { } },
    ];
    return (<ScrollView className="flex-1 bg-app-bg" contentContainerStyle={{ paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT }}>
      <View className="items-center pt-8 pb-6">
        <View className="relative">
          <Avatar src={avatarUrl} size={100} className="rounded-full"/>
          <StatusIndicator status="online" size={24} className="border-4 border-app-bg bottom-1 right-1"/>
        </View>
        <Text className="text-white text-2xl font-bold mt-4">{userName}</Text>
        <Text className="text-text-placeholder text-sm mt-1">{userDescription}</Text>
      </View>

      <View className="px-6 gap-3">
        
        <View className="bg-zinc-900 rounded-3xl p-5 gap-4">
          <View className="flex-row items-center gap-4">
            <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
              <AtIcon size={20} className="text-text-placeholder"/>
            </View>
            <View>
              <Text className="text-text-placeholder text-xs">Имя пользователя</Text>
              <Text className="text-white text-sm font-medium">{userHandle}</Text>
            </View>
          </View>
          
          <View className="flex-row items-center gap-4">
            <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
              <EnvelopeSimpleIcon size={20} className="text-text-placeholder"/>
            </View>
            <View>
              <Text className="text-text-placeholder text-xs">Почта</Text>
              <Text className="text-white text-sm font-medium">{email}</Text>
            </View>
          </View>
        </View>

        
        <View className="bg-zinc-900 rounded-3xl overflow-hidden">
          {menuItems.map((item, index) => (<Pressable key={index} onPress={item.onPress} className={`flex-row items-center justify-between p-5 ${index !== menuItems.length - 1 ? "border-b border-white/5" : ""}`}>
              <View className="flex-row items-center gap-4">
                <item.icon size={22} className="text-text-placeholder"/>
                <Text className="text-white text-base font-medium">{item.label}</Text>
              </View>
              <CaretRightIcon size={18} className="text-text-disabled"/>
            </Pressable>))}
        </View>

        
        <Pressable className="bg-zinc-900 rounded-3xl p-5 flex-row items-center gap-4 mt-2">
          <SignOutIcon size={22} className="text-danger-600"/>
          <Text className="text-danger-600 text-base font-medium">Выйти</Text>
        </Pressable>
      </View>
    </ScrollView>);
};
