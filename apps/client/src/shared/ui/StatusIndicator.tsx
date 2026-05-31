import React from "react";
import { View } from "react-native";
import { glow } from "./glow";

export type UserStatus = "online" | "away" | "doNotDisturb" | "offline";

interface StatusIndicatorProps {
    status: UserStatus;
    size?: number;
    className?: string;
}

const STATUS_COLORS: Record<UserStatus, string> = {
    online: "bg-success-600",
    away: "bg-warning-500",
    doNotDisturb: "bg-error-500",
    offline: "bg-text-placeholder",
};

export const StatusIndicator = ({ status, size = 12, className = "" }: StatusIndicatorProps) => {
    const glowStyle =
        status === "online"
            ? glow.success
            : status === "away"
              ? glow.warning
              : status === "doNotDisturb"
                ? glow.danger
                : undefined;

    return (
        <View
            style={[{ width: size, height: size }, glowStyle]}
            className={`absolute rounded-full border-2 border-app-card ${STATUS_COLORS[status]} ${className}`}
        />
    );
};

