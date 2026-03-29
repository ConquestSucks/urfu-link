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
        online: "bg-[#00BC7D]",
        offline: "bg-[#62748E]",
    };
    const sizeStyles = {
        width: size,
        height: size,
    };
    return (<View style={sizeStyles} className={`absolute rounded-full border-2 border-[#0B1225] ${statusColors[status]} ${className}`}/>);
};
