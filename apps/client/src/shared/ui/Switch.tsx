import { AnimatedPressable, AnimatedView } from "@/shared/lib/nativewind-interop";
import React from "react";
import { View } from "react-native";
import {
  interpolateColor,
  useAnimatedStyle,
  useDerivedValue,
  withSpring,
} from "react-native-reanimated";

interface SwitchProps {
  value: boolean;
  onValueChange: (value: boolean) => void;
  disabled?: boolean;
}

export const Switch = ({ value, onValueChange, disabled }: SwitchProps) => {
  const progress = useDerivedValue(() => {
    return withSpring(value ? 1 : 0, {
      mass: 0.3,
      damping: 12,
      stiffness: 300,
    });
  });

  const animatedTrackStyle = useAnimatedStyle(
    () => ({
      backgroundColor: interpolateColor(
        progress.value,
        [0, 1],
        ["rgba(255, 255, 255, 0.1)", "#2B7FFF"],
      ),
      opacity: disabled ? 0.5 : 1,
    }),
    [disabled],
  );

  const animatedHandleStyle = useAnimatedStyle(() => ({
    transform: [{ translateX: progress.value * 24 }],
  }));

  return (
    <AnimatedPressable
      onPress={() => !disabled && onValueChange(!value)}
      disabled={disabled}
      style={animatedTrackStyle}
    >
      <View className="w-12 h-6 rounded-full p-0.5 justify-center">
        <AnimatedView style={animatedHandleStyle}>
          <View className="w-5 h-5 bg-white rounded-full shadow-sm elevation-2" />
        </AnimatedView>
      </View>
    </AnimatedPressable>
  );
};
