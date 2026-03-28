import { StyleProp, ViewStyle } from "react-native";
import { AnimatedStyle } from "react-native-reanimated";

export type AnimatedViewStyle = StyleProp<AnimatedStyle<StyleProp<ViewStyle>>>;