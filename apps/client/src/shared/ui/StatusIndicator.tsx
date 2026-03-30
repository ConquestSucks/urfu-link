import React from "react";
import { View } from "react-native";
type UserStatus = "online" | "offline";
interface StatusIndicatorProps {
    status: UserStatus;
    size?: number;
    className?: string;
}
export const StatusIndicator = ({ status, size = 12, className = "", }: StatusIndicatorProps) => {
    const statusColors = {
        online: "bg-success-600",
        offline: "bg-text-placeholder",
    };
    const sizeStyles = {
        width: size,
        height: size,
    };
    return (<View style={sizeStyles} className={`absolute rounded-full border-2 border-app-card ${statusColors[status]} ${className}`}/>);
};
