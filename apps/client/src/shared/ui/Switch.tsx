import React from "react";
import { Pressable } from "react-native";
import Animated, {
  interpolateColor,
  useAnimatedStyle,
  useDerivedValue,
  withSpring,
} from "react-native-reanimated";

const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

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

  const animatedTrackStyle = useAnimatedStyle(() => ({
    backgroundColor: interpolateColor(
      progress.value,
      [0, 1],
      ["#314158", "#2B7FFF"],
    ),
  }));

  const animatedHandleStyle = useAnimatedStyle(() => ({
    transform: [{ translateX: progress.value * 24 }],
  }));

  return (
    <AnimatedPressable
      onPress={() => !disabled && onValueChange(!value)}
      disabled={disabled}
      style={[
        animatedTrackStyle,
        {
          width: 48,
          height: 24,
          borderRadius: 14,
          padding: 2,
          justifyContent: "center",
          opacity: disabled ? 0.5 : 1,
        },
      ]}
    >
      <Animated.View
        style={[
          animatedHandleStyle,
          {
            width: 20,
            height: 20,
            backgroundColor: "white",
            borderRadius: 10,
            shadowColor: "#000",
            shadowOffset: { width: 0, height: 1 },
            shadowOpacity: 0.15,
            shadowRadius: 2,
            elevation: 2,
          },
        ]}
        className="shadow-[0_10px_15px_-3px_rgba(0,0,0,0.1),0_4px_6px_-4px_rgba(0,0,0,0.1)]"
      />
    </AnimatedPressable>
  );
};
