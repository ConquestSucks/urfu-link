import type { Href } from "expo-router";
import type { IconProps } from "@/shared/ui/phosphor";
import type { ComponentType } from "react";
export interface TabItemProps {
    icon: ComponentType<IconProps>;
    label: string;
    isActive: boolean;
    onPress: () => void;
}
export interface TabConfig {
    icon: ComponentType<IconProps>;
    label: string;
    href: Href;
}
