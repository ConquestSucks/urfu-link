import type { Href } from "expo-router";
import { IconProps } from "phosphor-react-native";
export interface TabItemProps {
    icon: React.FC<IconProps>;
    label: string;
    isActive: boolean;
    onPress: () => void;
}
export interface TabConfig {
    icon: React.FC<IconProps>;
    label: string;
    href: Href;
}
