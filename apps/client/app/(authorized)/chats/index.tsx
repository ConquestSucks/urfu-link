import { UsersIcon } from "@/shared/ui/phosphor";
import { Text, View } from "react-native";
export default function ChatsScreen() {
    return (<View className="flex-1 bg-app-card items-center justify-center">
      <View className="items-center gap-6">
        <View className="p-5 bg-white/5 rounded-3xl">
          <UsersIcon size={40} className="text-text-disabled" weight="thin"/>
        </View>
        <Text className="text-text-secondary font-medium text-xl">Выберите чат</Text>
      </View>
    </View>);
}
