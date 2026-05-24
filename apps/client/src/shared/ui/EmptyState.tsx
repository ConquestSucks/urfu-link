import React, { ComponentType, ReactNode } from "react";
import { Text, View } from "react-native";
import { twMerge } from "tailwind-merge";
import type { IconProps } from "@/shared/ui/phosphor-types";

export interface EmptyStateProps {
    title: string;
    description?: string;
    icon?: ComponentType<IconProps>;
    size?: "compact" | "full";
    action?: ReactNode;
    className?: string;
}

export const EmptyState = ({
    title,
    description,
    icon: Icon,
    size = "full",
    action,
    className,
}: EmptyStateProps) => {
    if (size === "compact") {
        return (
            <View
                className={twMerge(
                    "py-8 items-center justify-center",
                    className,
                )}
            >
                {Icon && (
                    <Icon
                        size={20}
                        weight="regular"
                        className="text-text-disabled mb-2"
                    />
                )}
                <Text className="text-text-muted text-sm text-center">
                    {title}
                </Text>
            </View>
        );
    }

    return (
        <View
            className={twMerge(
                "flex-1 items-center justify-center py-20 px-6 min-h-[280px]",
                className,
            )}
        >
            {Icon && (
                <View className="w-24 h-24 rounded-full bg-white/5 items-center justify-center mb-5">
                    <Icon
                        size={40}
                        weight="regular"
                        className="text-text-disabled"
                    />
                </View>
            )}
            <Text className="text-text-muted text-base font-medium text-center">
                {title}
            </Text>
            {description && (
                <Text className="text-text-subtle text-sm text-center mt-2 max-w-[280px]">
                    {description}
                </Text>
            )}
            {action && <View className="mt-6">{action}</View>}
        </View>
    );
};
