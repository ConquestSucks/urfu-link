import type { AnimatedViewStyle } from "@/shared/types";
import { useMemo, useState } from "react";
import { Platform, ViewStyle } from "react-native";
import { Easing, Extrapolation, interpolate, useAnimatedStyle, useSharedValue, withTiming, } from "react-native-reanimated";
export const MIN_WIDTH = 87;
export const MAX_WIDTH = 260;
const ANIMATION_DURATION = 300;
const SIDEBAR_EASING = "cubic-bezier(0.4, 0, 0.2, 1)";
const IS_WEB = Platform.OS === "web";

type WebTransitionStyle = ViewStyle & {
    pointerEvents?: "auto" | "none";
    transitionDuration?: string;
    transitionProperty?: string;
    transitionTimingFunction?: string;
    userSelect?: "none";
    willChange?: string;
};

const webTransition = (transitionProperty: string): WebTransitionStyle =>
    IS_WEB
        ? {
              transitionDuration: `${ANIMATION_DURATION}ms`,
              transitionProperty,
              transitionTimingFunction: SIDEBAR_EASING,
          }
        : {};

export const useSidebarAnimation = () => {
    const [isCollapsed, setIsCollapsed] = useState(false);
    const sidebarWidth = useSharedValue(MAX_WIDTH);
    const handleToggle = () => {
        const nextCollapsed = !isCollapsed;
        setIsCollapsed(nextCollapsed);
        if (!IS_WEB) {
            sidebarWidth.value = withTiming(nextCollapsed ? MIN_WIDTH : MAX_WIDTH, {
                duration: ANIMATION_DURATION,
                easing: Easing.bezier(0.4, 0, 0.2, 1),
            });
        }
    };
    const sidebarAnimatedStyle = useAnimatedStyle((): ViewStyle => ({
        width: sidebarWidth.value,
        userSelect: "none",
    }));
    const textReanimatedStyle = useAnimatedStyle((): ViewStyle => {
        const opacity = interpolate(sidebarWidth.value, [MIN_WIDTH, 160, MAX_WIDTH], [0, 0, 1], Extrapolation.CLAMP);
        const maxWidth = interpolate(sidebarWidth.value, [MIN_WIDTH, 160, MAX_WIDTH], [0, 0, 250], Extrapolation.CLAMP);
        return {
            opacity,
            maxWidth,
            display: sidebarWidth.value <= MIN_WIDTH ? "none" : "flex",
            overflow: "hidden",
        };
    });
    const chevronReanimatedStyle = useAnimatedStyle((): ViewStyle => {
        const rotation = interpolate(sidebarWidth.value, [MIN_WIDTH, MAX_WIDTH], [180, 0], Extrapolation.CLAMP);
        return {
            height: 20,
            justifyContent: "center",
            transform: [{ rotate: `${rotation}deg` }],
        };
    });
    const sidebarWebStyle = useMemo(
        (): WebTransitionStyle => ({
            flexShrink: 0,
            overflow: "hidden",
            userSelect: "none",
            width: isCollapsed ? MIN_WIDTH : MAX_WIDTH,
            willChange: "width",
            ...webTransition("width"),
        }),
        [isCollapsed],
    );
    const textWebStyle = useMemo(
        (): WebTransitionStyle => ({
            maxWidth: isCollapsed ? 0 : 250,
            opacity: isCollapsed ? 0 : 1,
            overflow: "hidden",
            pointerEvents: isCollapsed ? "none" : "auto",
            ...webTransition("opacity, max-width"),
        }),
        [isCollapsed],
    );
    const chevronWebStyle = useMemo(
        (): WebTransitionStyle => ({
            height: 20,
            justifyContent: "center",
            transform: [{ rotate: isCollapsed ? "180deg" : "0deg" }],
            ...webTransition("transform"),
        }),
        [isCollapsed],
    );
    return {
        handleToggle,
        sidebarStyle: IS_WEB ? (sidebarWebStyle as AnimatedViewStyle) : sidebarAnimatedStyle,
        textAnimatedStyle: IS_WEB ? (textWebStyle as AnimatedViewStyle) : textReanimatedStyle,
        chevronAnimatedStyle: IS_WEB ? (chevronWebStyle as AnimatedViewStyle) : chevronReanimatedStyle,
    };
};
