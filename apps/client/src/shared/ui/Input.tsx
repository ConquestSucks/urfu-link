import React from "react";
import { Text, TextInput, TextInputProps, View } from "react-native";

interface InputProps extends TextInputProps {
  error?: string;
  value: string;
  disabled: boolean;
}

export const Input = ({
  error,
  value,
  disabled,
  className,
  ...props
}: InputProps) => {
  return (
    <View className={`gap-2 w-full ${className}`}>
      <TextInput
        editable={!disabled}
        value={value}
        placeholderTextColor="#45556C"
        selectionColor="#2B7FFF"
        className={`
          bg-[#0B1225] border outline-none
          ${error ? "border-red-500/50" : "border-white/10"} 
          ${!disabled && "focus:border-[#2B7FFF]"}
          text-white px-4 py-3.5 rounded-xl text-[15px]
        `}
        {...props}
      />

      {error && <Text className="text-red-500 text-xs ml-1">{error}</Text>}
    </View>
  );
};
