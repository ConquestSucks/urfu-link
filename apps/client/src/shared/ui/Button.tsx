import React from "react";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { Pressable, Text, View } from "react-native";
interface ButtonProps {
    onPress: () => void;
    label: string;
    icon?: React.ReactNode;
    variant?: "primary" | "secondary" | "danger";
    isLoading?: boolean;
    className?: string;
}
export const Button = ({ onPress, label, icon, variant = "primary", isLoading, className, }: ButtonProps) => {
    const variantStyles = {
        primary: "bg-brand-600 hover:bg-brand-400 active:bg-brand-700 active:scale-[0.98] active:opacity-90",
        secondary: "bg-slate-800/40 hover:bg-slate-800/60 active:bg-slate-800/80 active:scale-[0.98] active:opacity-90",
        danger: "bg-danger-500/10 hover:bg-danger-500/20 active:bg-red-500/20  active:scale-[0.98] active:opacity-90",
    };
    const textStyles = {
        primary: "text-white",
        secondary: "text-text-secondary",
        danger: "text-danger-400",
    };
    return (<Pressable onPress={onPress} disabled={isLoading} className={`
        flex-row items-center justify-center h-fit 
        px-4 py-2 rounded-xl gap-2 transition-all
        ${variantStyles[variant]} 
        ${isLoading ? "opacity-70" : ""} 
        ${className || ""}
      `}>
      {isLoading ? (<ActivityIndicator className={variant === "primary" ? "text-white" : "text-text-muted"}/>) : (<>
          {icon && <View>{icon}</View>}
          <Text className={`font-medium text-sm select-none ${textStyles[variant]}`}>
            {label}
          </Text>
        </>)}
    </Pressable>);
};
