import { cssInterop } from "nativewind";
import { Pressable, View, Text } from "react-native";
import Animated from "react-native-reanimated";

cssInterop(Animated.View, { className: "style" });
cssInterop(Animated.Text, { className: "style" });

export const AnimatedView = Animated.createAnimatedComponent(View);
export const AnimatedText = Animated.createAnimatedComponent(Text);
export const AnimatedPressable = Animated.createAnimatedComponent(Pressable);
