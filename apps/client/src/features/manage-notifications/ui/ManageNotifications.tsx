import { useUIStore } from "@/shared/model";
import { SwitchCard } from "@/shared/ui";
import { useState } from "react";
import { ScrollView, Text, View } from "react-native";
import { NOTIFICATIONS_SETTINGS } from "../config/settings";
type NotificationKeys = (typeof NOTIFICATIONS_SETTINGS.directMessages.items)[number]["key"];
type NotificationForm = Record<NotificationKeys, boolean>;
export const ManageNotifications = () => {
    const [form, setForm] = useState<NotificationForm>({
        showOnlineStatus: true,
        ShowLastSeen: false,
        allowDirectMessages: true,
    });
    const { isPending, setPending } = useUIStore();
    const handleToggle = (field: NotificationKeys) => async (newValue: boolean) => {
        if (isPending)
            return;
        setPending(true);
        const previousValue = form[field];
        setForm((prev) => ({ ...prev, [field]: newValue }));
        try {
            await new Promise((resolve) => setTimeout(resolve, 1500));
        }
        catch (error) {
            console.error("Ошибка обновления уведомлений", error);
            setForm((prev) => ({ ...prev, [field]: previousValue }));
        }
        finally {
            setPending(false);
        }
    };
    return (<ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
      {Object.values(NOTIFICATIONS_SETTINGS).map((section, sectionIndex) => (<View key={sectionIndex} className="gap-3">
          <Text className="text-text-secondary text-sm font-semibold">
            {section.label}
          </Text>
          <View className="gap-3">
            {section.items.map((item) => (<SwitchCard key={item.key} label={item.label} description={item.description} value={form[item.key as NotificationKeys]} onValueChange={handleToggle(item.key as NotificationKeys)} disabled={isPending}/>))}
          </View>
        </View>))}
    </ScrollView>);
};
