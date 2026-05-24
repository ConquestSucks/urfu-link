import React, { useEffect, useState } from "react";
import { View } from "react-native";

interface TypingEllipsisProps {
    testID: string;
    className?: string;
    intervalMs?: number;
}

const getDotColor = (className: string) => {
    if (className.includes("text-brand-300")) return "#60A5FA";
    if (className.includes("text-text-muted")) return "#90A1B9";
    return "#8B8FA8";
};

const getLayoutClassName = (className: string) =>
    className
        .split(/\s+/)
        .filter((token) => token.startsWith("ml-") || token.startsWith("mr-") || token === "shrink-0")
        .join(" ");

export const TypingEllipsis = ({
    testID,
    className = "",
    intervalMs = 350,
}: TypingEllipsisProps) => {
    const [dotCount, setDotCount] = useState(1);
    const dotColor = getDotColor(className);
    const layoutClassName = getLayoutClassName(className);

    useEffect(() => {
        const timer = setInterval(() => {
            setDotCount((current) => current === 3 ? 1 : current + 1);
        }, intervalMs);

        return () => clearInterval(timer);
    }, [intervalMs]);

    return (
        <View
            testID={testID}
            className={`h-3 w-4 flex-row items-center gap-0.5 ${layoutClassName}`}
            style={{ transform: [{ translateY: 1 }] }}
            accessibilityLabel="..."
        >
            {[0, 1, 2].map((index) => (
                <View
                    key={index}
                    className={`h-1 w-1 rounded-full ${
                        index < dotCount ? "opacity-100" : "opacity-30"
                    }`}
                    style={{ backgroundColor: dotColor }}
                />
            ))}
        </View>
    );
};
