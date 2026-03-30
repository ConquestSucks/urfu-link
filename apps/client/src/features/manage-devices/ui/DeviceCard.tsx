import { Button } from "@/shared/ui";
import { DeviceMobileIcon, LaptopIcon } from "@/shared/ui/phosphor";
import { Text, View } from "react-native";
interface DeviceCardProps {
    platform: string;
    name: string;
    location: string;
    lastLogin: string;
    isActive: boolean;
    onPress: () => void;
}
export const DeviceCard = ({ platform, name, location, lastLogin, isActive, onPress, }: DeviceCardProps) => {
    return (<View className="flex-row items-center justify-between bg-app-bg border border-white/5 rounded-2xl p-5">
      <View className="flex-row gap-4 items-center">
        <View className="p-[14px] items-center justify-center bg-slate-800/40 rounded-xl">
          {(platform === "Android" || platform === "iOS") && (<DeviceMobileIcon size={20} className="text-text-muted"/>)}
          {platform === "Web" && <LaptopIcon size={20} className="text-text-muted"/>}
        </View>

        <View className="gap-[1.5px]">
          <View className="flex-row gap-2 items-center">
            <Text className="text-white text-sm font-semibold">{name}</Text>
            {isActive && (<View className="px-2 py-0.5 pb-1 rounded-lg bg-success-600/20">
                <Text className="text-success-500 font-medium leading-none">
                  Текущее
                </Text>
              </View>)}
          </View>
          <View className="gap-0.5">
            <Text className="text-xs text-text-placeholder">{location}</Text>
            <Text className="text-xs text-text-disabled">{lastLogin}</Text>
          </View>
        </View>
      </View>
      {!isActive && (<Button className="w-fit" label="Выйти" variant="danger" onPress={onPress}/>)}
    </View>);
};
