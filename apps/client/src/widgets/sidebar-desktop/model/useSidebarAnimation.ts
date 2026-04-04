import { useState } from "react";
import { ViewStyle } from "react-native";
import { Easing, Extrapolation, interpolate, useAnimatedStyle, useSharedValue, withTiming, } from "react-native-reanimated";
export const MIN_WIDTH = 87;
export const MAX_WIDTH = 260;
const ANIMATION_DURATION = 300;
export const useSidebarAnimation = () => {
    const [isCollapsed, setIsCollapsed] = useState(false);
    const sidebarWidth = useSharedValue(MAX_WIDTH);
    const handleToggle = () => {
        const nextCollapsed = !isCollapsed;
        setIsCollapsed(nextCollapsed);
        sidebarWidth.value = withTiming(nextCollapsed ? MIN_WIDTH : MAX_WIDTH, {
            duration: ANIMATION_DURATION,
            easing: Easing.bezier(0.4, 0, 0.2, 1),
        });
    };
    const sidebarStyle = useAnimatedStyle((): ViewStyle => ({
        width: sidebarWidth.value,
        userSelect: "none",
    }));
    const textAnimatedStyle = useAnimatedStyle((): ViewStyle => {
        const opacity = interpolate(sidebarWidth.value, [MIN_WIDTH, 160, MAX_WIDTH], [0, 0, 1], Extrapolation.CLAMP);
        const maxWidth = interpolate(sidebarWidth.value, [MIN_WIDTH, 160, MAX_WIDTH], [0, 0, 250], Extrapolation.CLAMP);
        return {
            opacity,
            maxWidth,
            display: sidebarWidth.value <= MIN_WIDTH ? "none" : "flex",
            overflow: "hidden",
        };
    });
    const chevronAnimatedStyle = useAnimatedStyle((): ViewStyle => {
        const rotation = interpolate(sidebarWidth.value, [MIN_WIDTH, MAX_WIDTH], [180, 0], Extrapolation.CLAMP);
        return {
            height: 20,
            justifyContent: "center",
            transform: [{ rotate: `${rotation}deg` }],
        };
    });
    return {
        handleToggle,
        sidebarStyle,
        textAnimatedStyle,
        chevronAnimatedStyle,
    };
};
