import { useCurrentUser, useDeleteAvatar, useUploadAvatar } from "@/entities/user";
import { Avatar, Button, Input, LabeledCard, Skeleton } from "@/shared/ui";
import type { UploadAvatarFile } from "@urfu-link/api-client";
import { Platform, ScrollView, View } from "react-native";

const getNativeAssetName = (fileName: string | null | undefined, mimeType: string | null | undefined) => {
  if (fileName) return fileName;

  const ext = mimeType?.split("/")[1] ?? "jpeg";
  return `avatar.${ext}`;
};

async function pickFile(): Promise<UploadAvatarFile | null> {
  if (Platform.OS === "web") {
    return new Promise((resolve) => {
      const input = document.createElement("input");
      input.type = "file";
      input.accept = "image/jpeg,image/png,image/webp";
      input.onchange = () => {
        resolve(input.files?.[0] ?? null);
      };
      input.click();
    });
  }

  const ImagePicker = await import("expo-image-picker");
  const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) return null;

  const result = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ["images"],
    allowsEditing: true,
    aspect: [1, 1],
    quality: 0.85,
  });

  if (result.canceled) return null;

  const asset = result.assets[0];
  return {
    name: getNativeAssetName(asset.fileName, asset.mimeType),
    type: asset.mimeType ?? "image/jpeg",
    uri: asset.uri,
  };
}

export const ManageAccount = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const uploadAvatar = useUploadAvatar();
  const deleteAvatar = useDeleteAvatar();

  if (isLoading) {
    return (
      <ScrollView contentContainerClassName="gap-4">
        <LabeledCard label="Ð¤Ð¾Ñ‚Ð¾ Ð¿Ñ€Ð¾Ñ„Ð¸Ð»Ñ">
          <View className="flex-row gap-4">
            <Skeleton
              testID="account-avatar-skeleton"
              style={{ width: 80, height: 80 }}
              className="!rounded-2xl shrink-0"
            />
            <View className="flex-row gap-2 items-center">
              <Skeleton className="h-10 w-24 rounded-xl" />
              <Skeleton className="h-10 w-20 rounded-xl bg-white/5" />
            </View>
          </View>
        </LabeledCard>

        <LabeledCard label="Ð˜Ð¼Ñ">
          <Skeleton testID="account-input-skeleton" className="h-12 w-full rounded-xl bg-white/5" />
        </LabeledCard>

        <LabeledCard label="Email">
          <Skeleton testID="account-input-skeleton" className="h-12 w-full rounded-xl bg-white/5" />
        </LabeledCard>
      </ScrollView>
    );
  }

  const handlePickAndUpload = () => {
    pickFile().then((file) => {
      if (file) uploadAvatar.mutate(file);
    });
  };

  return (
    <ScrollView contentContainerClassName="gap-4">
      <LabeledCard label="Фото профиля">
        <View className="flex-row gap-4">
          <Avatar className="!rounded-2xl" size={80} src={profile?.account.avatarUrl ?? undefined} name={profile?.identity.name} />
          <View className="flex-row gap-2 items-center">
            <Button
              label="Изменить"
              onPress={handlePickAndUpload}
              isLoading={uploadAvatar.isPending}
            />
            <Button
              label="Удалить"
              variant="secondary"
              onPress={() => deleteAvatar.mutate()}
              isLoading={deleteAvatar.isPending}
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
