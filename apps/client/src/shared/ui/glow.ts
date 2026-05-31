import type { ViewStyle } from "react-native";

type GlowStyle = ViewStyle & { boxShadow?: string };

export const glow = {
    surface:
        {
            boxShadow:
                "0 18px 48px rgba(0, 0, 0, 0.36), 0 0 32px rgba(43, 127, 255, 0.08)",
        },
    surfaceStrong:
        {
            boxShadow:
                "0 24px 64px rgba(0, 0, 0, 0.48), 0 0 44px rgba(43, 127, 255, 0.12)",
        },
    brand:
        {
            boxShadow:
                "0 0 22px rgba(43, 127, 255, 0.36), 0 10px 26px rgba(0, 0, 0, 0.24)",
        },
    brandSoft:
        {
            boxShadow:
                "0 0 18px rgba(43, 127, 255, 0.22), 0 8px 20px rgba(0, 0, 0, 0.18)",
        },
    brandSubtle:
        {
            boxShadow:
                "0 0 14px rgba(43, 127, 255, 0.16)",
        },
    brandHalo:
        {
            boxShadow:
                "0 0 28px rgba(43, 127, 255, 0.28)",
        },
    success:
        {
            boxShadow:
                "0 0 20px rgba(0, 212, 146, 0.28), 0 8px 20px rgba(0, 0, 0, 0.18)",
        },
    successSubtle:
        {
            boxShadow:
                "0 0 12px rgba(0, 212, 146, 0.12)",
        },
    danger:
        {
            boxShadow:
                "0 0 20px rgba(251, 44, 54, 0.28), 0 8px 20px rgba(0, 0, 0, 0.2)",
        },
    dangerSubtle:
        {
            boxShadow:
                "0 0 12px rgba(251, 44, 54, 0.12)",
        },
    warning:
        {
            boxShadow:
                "0 0 18px rgba(255, 185, 0, 0.22), 0 8px 20px rgba(0, 0, 0, 0.18)",
        },
    inputFocus:
        {
            boxShadow:
                "0 0 0 1px rgba(96, 165, 250, 0.34), 0 0 20px rgba(43, 127, 255, 0.18)",
        },
    unread:
        {
            boxShadow:
                "0 0 18px rgba(96, 165, 250, 0.15)",
        },
    badge:
        {
            boxShadow:
                "0 0 12px rgba(251, 44, 54, 0.42)",
        },
} satisfies Record<string, GlowStyle>;
