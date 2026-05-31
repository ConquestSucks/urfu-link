import { Text, View } from "react-native";
import { glow } from "./glow";
import { Skeleton } from "./Skeleton";
import { Switch } from "./Switch";
interface SwitchCardProps {
    label: string;
    description: string;
    value: boolean;
    onValueChange: (value: boolean) => void;
    disabled?: boolean;
}
export const SwitchCard = ({ label, description, value, onValueChange, disabled, }: SwitchCardProps) => {
    return (<View
      className={`flex-row items-center justify-between p-6 gap-3 bg-app-bg border rounded-2xl ${value ? "border-brand-300/20" : "border-white/5"} ${disabled ? "opacity-70" : ""}`}
      style={value ? glow.brandSubtle : glow.surface}
    >
      <View className="gap-[5px] flex-1">
        <Text className="text-white font-medium text-sm">{label}</Text>
        <Text className="text-text-placeholder text-xs">{description}</Text>
      </View>
      <Switch value={value} onValueChange={onValueChange} disabled={disabled}/>
    </View>);
};

export const SwitchCardSkeleton = () => {
    return (<View className="flex-row items-center justify-between p-6 gap-3 bg-app-bg border border-white/5 rounded-2xl" style={glow.surface}>
      <View className="gap-[7px] flex-1">
        <Skeleton className="h-3.5 w-44 max-w-[80%] rounded" />
        <Skeleton className="h-3 w-full rounded bg-white/5" />
      </View>
      <Skeleton className="h-7 w-12 rounded-full bg-white/5" />
    </View>);
};
