import { useCurrentUser, useUpdatePrivacy } from "@/entities/user";
import { SwitchCard } from "@/shared/ui";
import { ActivityIndicator, View, ScrollView } from "react-native";
import { PRIVACY_SETTINGS, type PrivacyField } from "../config/settings";

export const ManagePrivacy = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const updatePrivacy = useUpdatePrivacy();

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center">
        <ActivityIndicator />
      </View>
    );
  }

  const privacy = profile?.privacy;

  const handleToggle = (field: PrivacyField) => (newValue: boolean) => {
    if (!privacy) return;
    updatePrivacy.mutate({
      showOnlineStatus: field === "showOnlineStatus" ? newValue : privacy.showOnlineStatus,
      showLastVisitTime: field === "showLastVisitTime" ? newValue : privacy.showLastVisitTime,
    });
  };

  return (
    <View className="flex-1">
      <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
        {PRIVACY_SETTINGS.map((item) => (
          <SwitchCard
            key={item.key}
            label={item.label}
            description={item.description}
            value={privacy?.[item.key] ?? true}
            onValueChange={handleToggle(item.key)}
            disabled={updatePrivacy.isPending}
          />
        ))}
      </ScrollView>
    </View>
  );
};
