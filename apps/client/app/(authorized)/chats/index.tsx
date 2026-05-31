import { UsersIcon } from "@/shared/ui/phosphor";
import { glow } from "@/shared/ui/glow";
import { Text, View } from "react-native";
export default function ChatsScreen() {
    return (<View className="flex-1 bg-app-card items-center justify-center">
      <View className="items-center gap-6">
        <View
          className="p-5 bg-brand-650/10 border border-brand-300/10 rounded-3xl"
          style={glow.brandHalo}
        >
          <UsersIcon size={40} className="text-brand-300" weight="thin"/>
        </View>
        <Text className="text-text-secondary font-medium text-xl">Выберите чат</Text>
      </View>
    </View>);
}
