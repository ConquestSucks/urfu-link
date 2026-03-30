import { useCurrentUser } from "@/entities/user";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/mobile-bottom-tabs/config/layout";
import { Avatar, StatusIndicator } from "@/shared/ui";
import {
  AtIcon,
  BellIcon,
  CaretRightIcon,
  EnvelopeSimpleIcon,
  MonitorIcon,
  ShieldCheckIcon,
  SignOutIcon,
  SpeakerHighIcon,
} from "phosphor-react-native";
import React from "react";
import { Pressable, Text, View, ScrollView } from "react-native";
import { router, type Href } from "expo-router";

export const ProfileMobile = () => {
  const { data: profile } = useCurrentUser();

  const userName = profile?.identity.name ?? "";
  const userDescription = profile?.account.aboutMe ?? "";
  const email = profile?.identity.email ?? "";
  const avatarUrl = profile?.account.avatarUrl ?? undefined;
  const userHandle = profile ? `@${profile.identity.username}` : "";

  const menuItems = [
    {
      icon: ShieldCheckIcon,
      label: "Приватность",
      onPress: () => router.push("/profile/privacy"),
    },
    { icon: MonitorIcon, label: "Устройства", onPress: () => {} },
    {
      icon: BellIcon,
      label: "Уведомления",
      onPress: () => router.push("/profile/notifications" as Href),
    },
    { icon: SpeakerHighIcon, label: "Звук и видео", onPress: () => {} },
  ];

  return (
    <ScrollView
      className="flex-1 bg-[#080D1D]"
      contentContainerStyle={{ paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT }}
    >
      <View className="items-center pt-8 pb-6">
        <View className="relative">
          <Avatar src={avatarUrl} size={100} className="rounded-full" />
          <StatusIndicator
            status="online"
            size={24}
            className="border-4 border-[#080D1D] bottom-1 right-1"
          />
        </View>
        <Text className="text-white text-2xl font-bold mt-4">{userName}</Text>
        <Text className="text-[#62748E] text-sm mt-1">{userDescription}</Text>
      </View>

      <View className="px-6 gap-3">
        <View className="bg-[#111827] rounded-3xl p-5 gap-4">
          <View className="flex-row items-center gap-4">
            <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
              <AtIcon size={20} color="#62748E" />
            </View>
            <View>
              <Text className="text-[#62748E] text-xs">Имя пользователя</Text>
              <Text className="text-white text-sm font-medium">{userHandle}</Text>
            </View>
          </View>

          <View className="flex-row items-center gap-4">
            <View className="w-10 h-10 bg-white/5 rounded-xl items-center justify-center">
              <EnvelopeSimpleIcon size={20} color="#62748E" />
            </View>
            <View>
              <Text className="text-[#62748E] text-xs">Почта</Text>
              <Text className="text-white text-sm font-medium">{email}</Text>
            </View>
          </View>
        </View>

        <View className="bg-[#111827] rounded-3xl overflow-hidden">
          {menuItems.map((item, index) => (
            <Pressable
              key={index}
              onPress={item.onPress}
              className={`flex-row items-center justify-between p-5 ${
                index !== menuItems.length - 1 ? "border-b border-white/5" : ""
              }`}
            >
              <View className="flex-row items-center gap-4">
                <item.icon size={22} color="#62748E" />
                <Text className="text-white text-base font-medium">
                  {item.label}
                </Text>
              </View>
              <CaretRightIcon size={18} color="#45556C" />
            </Pressable>
          ))}
        </View>

        <Pressable className="bg-[#111827] rounded-3xl p-5 flex-row items-center gap-4 mt-2">
          <SignOutIcon size={22} color="#EF4444" />
          <Text className="text-[#EF4444] text-base font-medium">Выйти</Text>
        </Pressable>
      </View>
    </ScrollView>
  );
};
