import { useEffect, useRef, useState } from "react";
import { Animated, Easing as NativeEasing } from "react-native";

type Listener = () => void;
type AnimationValue<T> = { __animationValue: T };
type InterpolationMode = "clamp" | "extend" | "identity";

const listeners = new Set<Listener>();

const notifyListeners = () => {
    listeners.forEach((listener) => listener());
};

const subscribe = (listener: Listener) => {
    listeners.add(listener);
    return () => {
        listeners.delete(listener);
    };
};

const resolveAnimationValue = <T>(value: T | AnimationValue<T>): T =>
    value &&
    typeof value === "object" &&
    "__animationValue" in value
        ? (value as AnimationValue<T>).__animationValue
        : value as T;

const createAnimationValue = <T>(value: T): AnimationValue<T> => ({
    __animationValue: resolveAnimationValue(value),
});

const normalizeExtrapolation = (extrapolate?: InterpolationMode) =>
    extrapolate ?? "extend";

const interpolateValue = (
    value: number,
    inputRange: readonly number[],
    outputRange: readonly number[],
    extrapolate?: InterpolationMode,
) => {
    const mode = normalizeExtrapolation(extrapolate);
    const lastInputIndex = inputRange.length - 1;
    if (lastInputIndex <= 0) return outputRange[0] ?? 0;

    let segmentIndex = 0;
    while (
        segmentIndex < lastInputIndex - 1 &&
        value > inputRange[segmentIndex + 1]
    ) {
        segmentIndex += 1;
    }

    const inputMin = inputRange[segmentIndex];
    const inputMax = inputRange[segmentIndex + 1];
    const outputMin = outputRange[segmentIndex];
    const outputMax = outputRange[segmentIndex + 1];

    if (mode === "identity" && (value < inputMin || value > inputMax)) {
        return value;
    }

    const denominator = inputMax - inputMin;
    const progress = denominator === 0 ? 0 : (value - inputMin) / denominator;
    const next = outputMin + progress * (outputMax - outputMin);

    if (mode !== "clamp") return next;
    return outputMin < outputMax
        ? Math.min(outputMax, Math.max(outputMin, next))
        : Math.min(outputMin, Math.max(outputMax, next));
};

const createLayoutAnimationBuilder = () => {
    const builder = {
        delay: () => builder,
        duration: () => builder,
        easing: () => builder,
        springify: () => builder,
        withCallback: () => builder,
        withInitialValues: () => builder,
    };

    return builder;
};

export type AnimatedStyle<T> = T;

export const Extrapolation = {
    CLAMP: "clamp",
    EXTEND: "extend",
    IDENTITY: "identity",
} as const;

export const Easing = NativeEasing;

export const FadeIn = createLayoutAnimationBuilder();
export const FadeInDown = createLayoutAnimationBuilder();
export const FadeInRight = createLayoutAnimationBuilder();

export const createAnimatedComponent = Animated.createAnimatedComponent;

export const interpolate = (
    value: number,
    inputRange: readonly number[],
    outputRange: readonly number[],
    extrapolate?: InterpolationMode,
) => interpolateValue(value, inputRange, outputRange, extrapolate);

export const useSharedValue = <T>(initialValue: T) => {
    const ref = useRef<{ value: T } | null>(null);

    if (!ref.current) {
        let currentValue = initialValue;
        const sharedValue = {} as { value: T };

        Object.defineProperty(sharedValue, "value", {
            get: () => currentValue,
            set: (nextValue: T | AnimationValue<T>) => {
                currentValue = resolveAnimationValue(nextValue);
                notifyListeners();
            },
        });

        ref.current = sharedValue;
    }

    return ref.current;
};

export const useAnimatedStyle = <T extends object>(updater: () => T): T => {
    const [style, setStyle] = useState(updater);

    useEffect(
        () => subscribe(() => setStyle(updater())),
        [updater],
    );

    return style;
};

export const withDelay = <T>(_: number, value: T) => createAnimationValue(value);
export const withRepeat = <T>(value: T) => createAnimationValue(value);
export const withSpring = <T>(value: T) => createAnimationValue(value);
export const withTiming = <T>(value: T) => createAnimationValue(value);

export default Animated;
