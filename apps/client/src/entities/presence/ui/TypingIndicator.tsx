import React from "react";
import { View, Text } from "react-native";
import { TypingEllipsis } from "@/shared/ui/TypingEllipsis";
import { useConversationTypers } from "../model/presence-store";

interface TypingIndicatorProps {
    conversationId: string;
    showNames?: boolean;
    excludeUserId?: string | null;
    variant?: "status" | "bubble";
}

const TypingDots = ({
    testID,
    className,
}: {
    testID: string;
    className: string;
}) => (
    <TypingEllipsis testID={testID} className={`ml-1 leading-none ${className}`} />
);

const StatusTypingDots = () => (
    <TypingEllipsis
        testID="typing-inline-dots"
        className="ml-0.5 text-brand-300 text-xs font-bold leading-none"
    />
);

export const TypingIndicator = ({
    conversationId,
    showNames = false,
    excludeUserId = null,
    variant = "status",
}: TypingIndicatorProps) => {
    const typers = useConversationTypers(conversationId, { excludeUserId });

    if (typers.length === 0) return null;

    const label = () => {
        if (!showNames) return "Печатает";
        if (typers.length === 1) {
            return typers[0].displayName
                ? `${typers[0].displayName} печатает`
                : "Печатает";
        }
        return `${typers.length} человека печатают`;
    };

    if (variant === "bubble") {
        return (
            <View
                testID="typing-indicator-bubble"
                className="self-start max-w-[78%] rounded-2xl rounded-bl-md bg-white/5 border border-white/5 px-3 py-2"
            >
                <View className="flex-row items-center min-w-0">
                    <Text
                        numberOfLines={1}
                        className="text-text-muted text-xs font-medium leading-none"
                    >
                        {label()}
                    </Text>
                    <TypingDots
                        testID="typing-bubble-dots"
                        className="text-text-muted text-xs font-bold"
                    />
                </View>
            </View>
        );
    }

    return (
        <View testID="typing-indicator" className="flex-row items-center min-w-0">
            <Text numberOfLines={1} className="text-brand-300 text-xs font-medium leading-none">
                {label()}
            </Text>
            <StatusTypingDots />
        </View>
    );
};
