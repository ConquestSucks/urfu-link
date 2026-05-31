import { cssInterop } from "nativewind";
import { Pressable, View, Text } from "react-native";
import Animated from "react-native-reanimated";

export const AnimatedView = Animated.createAnimatedComponent(View);
export const AnimatedText = Animated.createAnimatedComponent(Text);
export const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

cssInterop(AnimatedView, { className: "style" });
cssInterop(AnimatedText, { className: "style" });
cssInterop(AnimatedPressable, { className: "style" });
