import React from "react";
import { View } from "react-native";

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
    return (
        <View
            style={{ width: size, height: size }}
            className={`absolute rounded-full border-2 border-app-card ${STATUS_COLORS[status]} ${className}`}
        />
    );
};

