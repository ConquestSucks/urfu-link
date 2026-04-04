import { Image } from "expo-image";
import React from "react";
import { Text, View } from "react-native";

interface AvatarProps {
    size?: number;
    src?: string | null;
    name?: string;
    className?: string;
}

const AVATAR_COLORS = [
    "#4F46E5",
    "#7C3AED",
    "#DB2777",
    "#D97706",
    "#059669",
    "#0284C7",
    "#DC2626",
    "#0891B2",
];

function getInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return "?";
    if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
    return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
}

function getAvatarColor(name: string): string {
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
        hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

export const Avatar = ({ size = 40, src, name, className }: AvatarProps) => {
    if (!src) {
        if (name) {
            return (
                <View
                    style={{ width: size, height: size, backgroundColor: getAvatarColor(name) }}
                    className={`rounded-xl shrink-0 items-center justify-center ${className ?? ""}`}
                >
                    <Text style={{ fontSize: size * 0.35, color: "white", fontWeight: "600" }}>
                        {getInitials(name)}
                    </Text>
                </View>
            );
        }
        return (
            <View
                style={{ width: size, height: size }}
                className={`rounded-xl bg-slate-750 shrink-0 ${className ?? ""}`}
            />
        );
    }
    return (
        <Image
            source={{ uri: src }}
            style={{ width: size, height: size }}
            className={`rounded-xl shrink-0 ${className ?? ""}`}
            contentFit="cover"
            transition={200}
        />
    );
};
