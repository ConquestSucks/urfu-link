import { UsersIcon } from "phosphor-react-native";
import { Text, View } from "react-native";
export default function ChatsScreen() {
    return (<View className="flex-1 bg-[#0B1225] items-center justify-center">
      <View className="items-center gap-6">
        <View className="p-5 bg-white/5 rounded-3xl">
          <UsersIcon size={40} color="#45556C" weight="thin"/>
        </View>
        <Text className="text-[#CAD5E2] font-medium text-xl">Выберите чат</Text>
      </View>
    </View>);
}
