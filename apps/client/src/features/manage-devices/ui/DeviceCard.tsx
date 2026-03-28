import { Button } from "@/shared/ui";
import { Laptop, Smartphone } from "lucide-react-native";
import { Text, View } from "react-native";

interface DeviceCardProps {
  platform: string;
  name: string;
  location: string;
  lastLogin: string;
  isActive: boolean;
  onPress: () => void;
}

export const DeviceCard = ({
  platform,
  name,
  location,
  lastLogin,
  isActive,
  onPress,
}: DeviceCardProps) => {
  return (
    <View className="flex-row items-center justify-between bg-[#080D1D] border border-white/5 rounded-2xl p-5">
      <View className="flex-row gap-4 items-center">
        <View className="p-[14px] items-center justify-center bg-[#1D293D]/40 rounded-xl">
          {(platform === "Android" || platform === "iOS") && (
            <Smartphone size={20} color={"#90A1B9"} />
          )}
          {platform === "Web" && <Laptop size={20} color={"#90A1B9"} />}
        </View>

        <View className="gap-[1.5px]">
          <View className="flex-row gap-2 items-center">
            <Text className="text-white text-sm font-semibold">{name}</Text>
            {isActive && (
              <View className="px-2 py-0.5 pb-1 rounded-lg bg-[#00BC7D]/20">
                <Text className="text-[#00D492] font-medium leading-none">
                  Текущее
                </Text>
              </View>
            )}
          </View>
          <View className="gap-0.5">
            <Text className="text-xs text-[#62748E]">{location}</Text>
            <Text className="text-xs text-[#45556C]">{lastLogin}</Text>
          </View>
        </View>
      </View>
      {!isActive && (
        <Button
          className="w-fit"
          label="Выйти"
          variant="danger"
          onPress={onPress}
        />
      )}
    </View>
  );
};
