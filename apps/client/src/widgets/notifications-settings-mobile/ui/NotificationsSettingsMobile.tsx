import { safeGoBack } from "@/shared/lib/safeGoBack";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/mobile-bottom-tabs/config/layout";
import { SwitchCard } from "@/shared/ui";
import { CaretLeftIcon } from "phosphor-react-native";
import React, { useState } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";
export const NotificationsSettingsMobile = () => {
    const [personalNewMsg, setPersonalNewMsg] = useState(true);
    const [personalSound, setPersonalSound] = useState(true);
    const [subjectNotify, setSubjectNotify] = useState(true);
    const [mentionsOnly, setMentionsOnly] = useState(false);
    const [dnd, setDnd] = useState(false);
    return (<View className="flex-1 bg-[#080D1D]">
      <View className="flex-row items-center px-6 py-8 border-b border-white/5">
        <Pressable onPress={() => safeGoBack("/profile")} className="mr-6" hitSlop={8}>
          <CaretLeftIcon size={24} color="#FFFFFF"/>
        </Pressable>
        <Text className="text-white text-2xl font-bold">Уведомления</Text>
      </View>

      <ScrollView className="flex-1" contentContainerStyle={{ padding: 24, paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT, gap: 16 }} showsVerticalScrollIndicator={false}>
        <Text className="text-[#62748E] text-xs font-bold uppercase tracking-wide">
          Личные чаты
        </Text>
        <SwitchCard label="Уведомления о новых сообщениях" description="Получать уведомления при получении личных сообщений" value={personalNewMsg} onValueChange={setPersonalNewMsg}/>
        <SwitchCard label="Звук уведомлений" description="Воспроизводить звук при новом сообщении" value={personalSound} onValueChange={setPersonalSound}/>

        <Text className="text-[#62748E] text-xs font-bold uppercase tracking-wide mt-2">
          Дисциплины
        </Text>
        <SwitchCard label="Уведомления от дисциплин" description="Получать уведомления о сообщениях в чатах дисциплин" value={subjectNotify} onValueChange={setSubjectNotify}/>
        <SwitchCard label="Упоминания" description="Уведомлять только когда меня упоминают" value={mentionsOnly} onValueChange={setMentionsOnly}/>

        <Text className="text-[#62748E] text-xs font-bold uppercase tracking-wide mt-2">
          Режим «Не беспокоить»
        </Text>
        <SwitchCard label="Включить режим" description="Отключить все уведомления" value={dnd} onValueChange={setDnd}/>

        <View className="bg-[#111827] rounded-2xl p-5 border border-white/5 mt-2">
          <Text className="text-[#62748E] text-sm leading-5">
            Настройте уведомления так, чтобы не пропустить важные учебные события и
            сообщения от одногруппников, сохраняя при этом фокус на учебе.
          </Text>
        </View>
      </ScrollView>
    </View>);
};
