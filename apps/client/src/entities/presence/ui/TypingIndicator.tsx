import React, { useEffect, useRef } from "react";
import { Animated, View, Text } from "react-native";
import { useConversationTypers } from "../model/presence-store";

interface TypingIndicatorProps {
    conversationId: string;
    showNames?: boolean;
}

const Dot = ({ delay }: { delay: number }) => {
    const opacity = useRef(new Animated.Value(0.3)).current;

    useEffect(() => {
        const animation = Animated.loop(
            Animated.sequence([
                Animated.delay(delay),
                Animated.timing(opacity, {
                    toValue: 1,
                    duration: 300,
                    useNativeDriver: true,
                }),
                Animated.timing(opacity, {
                    toValue: 0.3,
                    duration: 300,
                    useNativeDriver: true,
                }),
            ])
        );
        animation.start();
        return () => animation.stop();
    }, []);

    return (
        <Animated.View
            style={{ opacity }}
            className="w-1.5 h-1.5 rounded-full bg-text-muted"
        />
    );
};

export const TypingIndicator = ({ conversationId, showNames = false }: TypingIndicatorProps) => {
    const typers = useConversationTypers(conversationId);

    if (typers.length === 0) return null;

    const label = () => {
        if (!showNames) return "Печатает...";
        if (typers.length === 1) {
            return typers[0].displayName
                ? `${typers[0].displayName} печатает...`
                : "Печатает...";
        }
        return `${typers.length} человека печатают...`;
    };

    return (
        <View className="flex-row items-center gap-1.5 px-4 py-1">
            <View className="flex-row gap-0.5 items-center">
                <Dot delay={0} />
                <Dot delay={150} />
                <Dot delay={300} />
            </View>
            <Text className="text-text-muted text-xs">{label()}</Text>
        </View>
    );
};
