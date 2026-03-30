import { safeGoBack } from "@/shared/lib/safeGoBack";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/mobile-bottom-tabs/config/layout";
import { SwitchCard } from "@/shared/ui";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useState } from "react";
import { Pressable, Text, View, ScrollView } from "react-native";
export const PrivacySettingsMobile = () => {
    const [settings, setSettings] = useState({
        showOnline: true,
        showLastSeen: true,
        whoCanWrite: true,
        showGroups: true,
    });
    return (<View className="flex-1 bg-app-bg">
      <View className="flex-row items-center px-6 py-8 border-b border-white/5">
        <Pressable onPress={() => safeGoBack("/profile")} className="mr-6">
          <CaretLeftIcon size={24} className="text-white"/>
        </Pressable>
        <Text className="text-white text-2xl font-bold">Приватность</Text>
      </View>

      <ScrollView className="flex-1" contentContainerStyle={{ padding: 24, paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT, gap: 16 }}>
        <SwitchCard label="Показывать статус онлайн" description="Другие пользователи смогут видеть, когда вы в сети" value={settings.showOnline} onValueChange={(val) => setSettings({ ...settings, showOnline: val })}/>
        
        <SwitchCard label="Показывать время последнего визита" description="Отображение времени последней активности" value={settings.showLastSeen} onValueChange={(val) => setSettings({ ...settings, showLastSeen: val })}/>

        <SwitchCard label="Кто может писать мне лично" description="Разрешить личные сообщения от всех студентов и преподавателей" value={settings.whoCanWrite} onValueChange={(val) => setSettings({ ...settings, whoCanWrite: val })}/>

        <SwitchCard label="Показывать мои группы" description="Другие пользователи смогут видеть ваши группы" value={settings.showGroups} onValueChange={(val) => setSettings({ ...settings, showGroups: val })}/>

        <View className="bg-zinc-900 rounded-3xl p-6 mt-4">
          <Text className="text-text-placeholder text-[13px] leading-[20px]">
            Эти настройки помогут вам контролировать, какую информацию о вас могут видеть другие пользователи URFU LINK. Изменения применяются мгновенно.
          </Text>
        </View>
      </ScrollView>
    </View>);
};
