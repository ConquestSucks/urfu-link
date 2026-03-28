import { useUIStore } from "@/shared/model";
import { SwitchCard } from "@/shared/ui";
import React, { useState } from "react";
import { ScrollView, View } from "react-native";
import { PRIVACY_SETTINGS, PrivacyForm } from "../config/settings";

export const ManagePrivacy = () => {
  const [form, setForm] = useState<PrivacyForm>({
    showOnlineStatus: true,
    ShowLastSeen: false,
    allowDirectMessages: true,
  });

  const { isPending, setPending } = useUIStore();

  const handleToggle =
    (field: keyof PrivacyForm) => async (newValue: boolean) => {
      if (isPending) return;

      setPending(true);
      const previousValue = form[field];

      setForm((prev) => ({ ...prev, [field]: newValue }));

      try {
        await new Promise((resolve) => setTimeout(resolve, 1500));
      } catch (error) {
        console.error("Ошибка обновления:", error);
        setForm((prev) => ({ ...prev, [field]: previousValue }));
      } finally {
        setPending(false);
      }
    };

  return (
    <View className="flex-1">
      <ScrollView
        contentContainerClassName="gap-4"
        showsVerticalScrollIndicator={false}
      >
        {PRIVACY_SETTINGS.map((item) => (
          <SwitchCard
            key={item.key}
            label={item.label}
            description={item.description}
            value={form[item.key]}
            onValueChange={handleToggle(item.key)}
            disabled={isPending}
          />
        ))}
      </ScrollView>
    </View>
  );
};
