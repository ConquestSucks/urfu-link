import { Button } from "@/shared/ui";
import { Text, View } from "react-native";
interface EndSessionsProps {
    onPress: () => void;
}
export const EndSessions = ({ onPress }: EndSessionsProps) => {
    return (<View className="gap-4 bg-[#080D1D] border border-[#FB2C36]/20 rounded-2xl p-5">
      <View className="gap-2">
        <Text className="text-sm text-white text-medium">
          Завершить все сеансы
        </Text>
        <Text className="text-xs text-[#90A1B9]">
          Выйти из аккаунта на всех устройствах кроме текущего
        </Text>
      </View>
      <Button label="Завершить все сеансы" variant="danger" className="w-fit" onPress={onPress}/>
    </View>);
};
