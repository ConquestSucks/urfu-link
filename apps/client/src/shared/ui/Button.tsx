import React from "react";
import { ActivityIndicator, Pressable, Text, View } from "react-native";
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
        primary: "bg-[#2B7FFF] hover:bg-[#3D8BFF] active:bg-[#1A6EEB] active:scale-[0.98] active:opacity-90",
        secondary: "bg-[#1D293D]/40 hover:bg-[#1D293D]/60 active:bg-[#1D293D]/80 active:scale-[0.98] active:opacity-90",
        danger: "bg-[#FB2C36]/10 hover:bg-[#FB2C36]/20 active:bg-red-500/20  active:scale-[0.98] active:opacity-90",
    };
    const textStyles = {
        primary: "text-white",
        secondary: "text-[#CAD5E2]",
        danger: "text-[#FF6467]",
    };
    return (<Pressable onPress={onPress} disabled={isLoading} className={`
        flex-row items-center justify-center h-fit 
        px-4 py-2 rounded-xl gap-2 transition-all
        ${variantStyles[variant]} 
        ${isLoading ? "opacity-70" : ""} 
        ${className || ""}
      `}>
      {isLoading ? (<ActivityIndicator color={variant === "primary" ? "white" : "#90A1B9"}/>) : (<>
          {icon && <View>{icon}</View>}
          <Text className={`font-medium text-sm select-none ${textStyles[variant]}`}>
            {label}
          </Text>
        </>)}
    </Pressable>);
};
