import { useUserStore } from "@/entities/user";
import { Avatar, Button, Input, LabeledCard } from "@/shared/ui";
import { ScrollView, View } from "react-native";

export const ManageAccount = () => {
  const { userName, userDescription, email, avatarUrl } = useUserStore();

  return (
    <ScrollView contentContainerClassName="gap-4">
      <LabeledCard label="Фото профиля">
        <View className="flex-row gap-4">
          <Avatar className="!rounded-2xl" size={80} src={avatarUrl} />
          <View className="flex-row gap-2 items-center">
            <Button label="Изменить" onPress={() => {}} />
            <Button label="Удалить" variant="secondary" onPress={() => {}} />
          </View>
        </View>
      </LabeledCard>

      <LabeledCard label="Имя">
        <Input value={userName} disabled />
      </LabeledCard>

      <LabeledCard label="Email">
        <Input value={email} disabled />
      </LabeledCard>
    </ScrollView>
  );
};
