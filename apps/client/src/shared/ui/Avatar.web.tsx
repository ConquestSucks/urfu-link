import type { CSSProperties } from "react";
import React from "react";
import { Text, View } from "react-native";
import { getAvatarVariantSrcSet, getAvatarVariantUrl } from "@/shared/lib/avatar-url";

interface AvatarProps {
    size?: number;
    src?: string | null;
    name?: string;
    className?: string;
    accessibilityLabel?: string;
    decorative?: boolean;
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
const DEFAULT_AVATAR_RADIUS = 12;

const getAvatarBorderRadius = (size: number, className?: string) =>
    className?.split(/\s+/).includes("rounded-full")
        ? size / 2
        : Math.min(DEFAULT_AVATAR_RADIUS, size / 2);

const imageStyle: CSSProperties = {
    display: "block",
    width: "100%",
    height: "100%",
    objectFit: "cover",
    borderRadius: "inherit",
};

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

const getAccessibilityLabel = (
    name: string | undefined,
    accessibilityLabel: string | undefined,
    decorative: boolean | undefined,
) => {
    if (decorative) return "";
    if (accessibilityLabel !== undefined) return accessibilityLabel;
    return name ? `Avatar ${name}` : "User avatar";
};

export const Avatar = ({
    size = 40,
    src,
    name,
    className,
    accessibilityLabel,
    decorative,
}: AvatarProps) => {
    const label = getAccessibilityLabel(name, accessibilityLabel, decorative);
    const borderRadius = getAvatarBorderRadius(size, className);

    if (!src) {
        if (name) {
            return (
                <View
                    style={{
                        width: size,
                        height: size,
                        borderRadius,
                        backgroundColor: getAvatarColor(name),
                    }}
                    className={`rounded-xl shrink-0 items-center justify-center ${className ?? ""}`}
                    accessibilityLabel={label}
                    accessible={!decorative}
                >
                    <Text style={{ fontSize: size * 0.35, color: "white", fontWeight: "600" }}>
                        {getInitials(name)}
                    </Text>
                </View>
            );
        }

        return (
            <View
                style={{ width: size, height: size, borderRadius }}
                className={`rounded-xl bg-slate-750 shrink-0 ${className ?? ""}`}
                accessibilityLabel={label}
                accessible={!decorative}
            />
        );
    }

    const optimizedSrc = getAvatarVariantUrl(src, size);
    const srcSet = getAvatarVariantSrcSet(src);

    return (
        <View
            style={{ width: size, height: size, borderRadius }}
            className={`rounded-xl shrink-0 overflow-hidden ${className ?? ""}`}
        >
            <img
                src={optimizedSrc}
                srcSet={srcSet}
                sizes={`${Math.ceil(size)}px`}
                width={size}
                height={size}
                alt={decorative ? "" : label}
                aria-hidden={decorative || undefined}
                decoding="async"
                loading={size >= 80 ? "eager" : "lazy"}
                style={imageStyle}
            />
        </View>
    );
};
