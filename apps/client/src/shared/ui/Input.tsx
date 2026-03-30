import React from "react";
import { Text, TextInput, TextInputProps, View } from "react-native";
interface InputProps extends TextInputProps {
    error?: string;
    value: string;
    disabled: boolean;
}
export const Input = ({ error, value, disabled, className, ...props }: InputProps) => {
    return (<View className={`gap-2 w-full ${className}`}>
      <TextInput editable={!disabled} value={value} className={`
          bg-app-card border outline-none
          ${error ? "border-red-500/50" : "border-white/10"} 
          ${!disabled && "focus:border-brand-600"}
          text-white px-4 py-3.5 rounded-xl text-[15px]
          placeholder:text-text-disabled selection:text-brand-600 caret-brand-600
        `} {...props}/>

      {error && <Text className="text-red-500 text-xs ml-1">{error}</Text>}
    </View>);
};
