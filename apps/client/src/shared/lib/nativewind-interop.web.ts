import { cssInterop } from "nativewind";
import { Animated, Pressable, Text, View } from "react-native";

export const AnimatedView = Animated.createAnimatedComponent(View);
export const AnimatedText = Animated.createAnimatedComponent(Text);
export const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

cssInterop(AnimatedView, { className: "style" });
cssInterop(AnimatedText, { className: "style" });
cssInterop(AnimatedPressable, { className: "style" });
