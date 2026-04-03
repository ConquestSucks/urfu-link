import React, { useEffect, useState } from "react";
import { Pressable, View } from "react-native";

interface SwitchProps {
  value: boolean;
  onValueChange: (value: boolean) => void;
  disabled?: boolean;
}

export const Switch = ({ value, onValueChange, disabled }: SwitchProps) => {
  const [localValue, setLocalValue] = useState(value);

  useEffect(() => {
    setLocalValue(value);
  }, [value]);

  const handlePress = () => {
    if (disabled) return;
    const next = !value; // always based on server state to avoid race conditions
    setLocalValue(next); // optimistic visual update
    onValueChange(next);
  };

  return (
    <Pressable
      onPress={handlePress}
      disabled={disabled}
      style={{
        width: 48,
        height: 24,
        borderRadius: 12,
        padding: 2,
        justifyContent: "center",
        backgroundColor: localValue ? "#2B7FFF" : "rgba(255, 255, 255, 0.1)",
        opacity: disabled ? 0.5 : 1,
        // @ts-ignore
        transition: "background-color 200ms ease",
      }}
    >
      <View
        style={{
          width: 20,
          height: 20,
          borderRadius: 10,
          backgroundColor: "white",
          transform: [{ translateX: localValue ? 24 : 0 }],
          // @ts-ignore
          transition: "transform 200ms ease",
        }}
      />
    </Pressable>
  );
};
