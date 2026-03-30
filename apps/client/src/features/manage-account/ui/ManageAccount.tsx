import { useCurrentUser, useDeleteAvatar, useUploadAvatar } from "@/entities/user";
import { Avatar, Button, Input, LabeledCard } from "@/shared/ui";
import { ActivityIndicator, ScrollView, View } from "react-native";

export const ManageAccount = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const uploadAvatar = useUploadAvatar();
  const deleteAvatar = useDeleteAvatar();

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center">
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <ScrollView contentContainerClassName="gap-4">
      <LabeledCard label="Фото профиля">
        <View className="flex-row gap-4">
          <Avatar className="!rounded-2xl" size={80} src={profile?.account.avatarUrl ?? undefined} />
          <View className="flex-row gap-2 items-center">
            <Button
              label={uploadAvatar.isPending ? "Загрузка..." : "Изменить"}
              onPress={() => {
                // File picker — платформенная логика вне scope этого компонента
              }}
            />
            <Button
              label="Удалить"
              variant="secondary"
              onPress={() => deleteAvatar.mutate()}
            />
          </View>
        </View>
      </LabeledCard>

      <LabeledCard label="Имя">
        <Input value={profile?.identity.name ?? ""} disabled />
      </LabeledCard>

      <LabeledCard label="Email">
        <Input value={profile?.identity.email ?? ""} disabled />
      </LabeledCard>
    </ScrollView>
  );
};
