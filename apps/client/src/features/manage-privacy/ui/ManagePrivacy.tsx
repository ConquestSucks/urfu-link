import { useCurrentUser, useUpdatePrivacy } from "@/entities/user";
import { SwitchCard, SwitchCardSkeleton } from "@/shared/ui";
import { View, ScrollView } from "react-native";
import { PRIVACY_SETTINGS, type PrivacyField } from "../config/settings";

export const ManagePrivacy = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const updatePrivacy = useUpdatePrivacy();

  if (isLoading) {
    return (
      <View className="flex-1">
        <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
          {PRIVACY_SETTINGS.map((item) => (
            <SwitchCardSkeleton key={item.key} />
          ))}
        </ScrollView>
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
