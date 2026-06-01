import type { ViewStyle } from "react-native";

type GlowStyle = ViewStyle & { boxShadow?: string };

export const glow = {
    surface:
        {
            boxShadow:
                "0 8px 22px rgba(0, 0, 0, 0.24)",
        },
    surfaceStrong:
        {
            boxShadow:
                "0 12px 32px rgba(0, 0, 0, 0.30)",
        },
    brand:
        {
            boxShadow:
                "0 0 0 1px rgba(43, 127, 255, 0.22), 0 6px 16px rgba(0, 0, 0, 0.18)",
        },
    brandSoft:
        {
            boxShadow:
                "0 0 0 1px rgba(43, 127, 255, 0.16), 0 4px 12px rgba(0, 0, 0, 0.16)",
        },
    brandSubtle:
        {
            boxShadow:
                "0 0 0 1px rgba(43, 127, 255, 0.12)",
        },
    brandHalo:
        {
            boxShadow:
                "0 0 0 1px rgba(43, 127, 255, 0.16), 0 0 10px rgba(43, 127, 255, 0.10)",
        },
    success:
        {
            boxShadow:
                "0 0 0 1px rgba(0, 212, 146, 0.20), 0 4px 12px rgba(0, 0, 0, 0.14)",
        },
    successSubtle:
        {
            boxShadow:
                "0 0 0 1px rgba(0, 212, 146, 0.14)",
        },
    danger:
        {
            boxShadow:
                "0 0 0 1px rgba(251, 44, 54, 0.22), 0 4px 12px rgba(0, 0, 0, 0.16)",
        },
    dangerSubtle:
        {
            boxShadow:
                "0 0 0 1px rgba(251, 44, 54, 0.16)",
        },
    warning:
        {
            boxShadow:
                "0 0 0 1px rgba(255, 185, 0, 0.20), 0 4px 12px rgba(0, 0, 0, 0.14)",
        },
    inputFocus:
        {
            boxShadow:
                "0 0 0 1px rgba(96, 165, 250, 0.32), 0 0 0 3px rgba(43, 127, 255, 0.08)",
        },
    unread:
        {
            boxShadow:
                "inset 3px 0 0 rgba(96, 165, 250, 0.32)",
        },
    badge:
        {
            boxShadow:
                "0 0 0 1px rgba(251, 44, 54, 0.30)",
        },
} satisfies Record<string, GlowStyle>;
