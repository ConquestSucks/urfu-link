import { Text, View } from "react-native";
interface LabeledCard {
    label: string;
    children: React.ReactNode;
}
export const LabeledCard = ({ label, children }: LabeledCard) => {
    return (<View className="p-6 gap-3 bg-app-bg border border-white/5 rounded-2xl">
      <Text className="text-text-secondary font-medium text-sm">{label}</Text>
      {children}
    </View>);
};
