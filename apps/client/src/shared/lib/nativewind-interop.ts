import { cssInterop } from "nativewind";
import { Pressable } from "react-native";
import Animated from "react-native-reanimated";

export const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

cssInterop(AnimatedPressable, {
  className: "style",
});

cssInterop(Animated.View, {
  className: "style",
});

cssInterop(Animated.Text, {
  className: "style",
});
