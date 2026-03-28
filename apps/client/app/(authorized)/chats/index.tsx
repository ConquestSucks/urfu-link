import { Users } from "lucide-react-native";
import { Text, View } from "react-native";

export default function ChatsScreen() {
  return (
    <View className="flex-1 bg-[#0B1225] items-center justify-center">
      <View className="items-center gap-6">
        <View className="p-5 bg-white/5 rounded-3xl">
          <Users size={40} color="#45556C" strokeWidth={1} />
        </View>
        <Text className="text-[#CAD5E2] font-medium text-xl">Выберите чат</Text>
      </View>
    </View>
  );
}
