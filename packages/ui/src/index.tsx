import type { PropsWithChildren } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";

type ButtonTone = "primary" | "secondary";
type BadgeTone = "success" | "warning" | "neutral";

type AppButtonProps = {
  label: string;
  onPress?: () => void;
  disabled?: boolean;
  tone?: ButtonTone;
};

type AppBadgeProps = {
  label: string;
  tone?: BadgeTone;
};

type AppCardProps = PropsWithChildren<{
  title: string;
  subtitle?: string;
}>;

export function AppScreen({ children }: PropsWithChildren) {
  return <View style={styles.screen}>{children}</View>;
}

export function AppCard({ title, subtitle, children }: AppCardProps) {
  return (
    <View style={styles.card}>
      <View style={styles.cardHeader}>
        <Text style={styles.cardTitle}>{title}</Text>
        {subtitle ? <Text style={styles.cardSubtitle}>{subtitle}</Text> : null}
      </View>
      <View style={styles.cardBody}>{children}</View>
    </View>
  );
}

export function AppButton({ label, onPress, disabled, tone = "primary" }: AppButtonProps) {
  return (
    <Pressable
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        tone === "primary" ? styles.buttonPrimary : styles.buttonSecondary,
        pressed && !disabled ? styles.buttonPressed : null,
        disabled ? styles.buttonDisabled : null
      ]}
    >
      <Text style={styles.buttonText}>{label}</Text>
    </Pressable>
  );
}

export function AppBadge({ label, tone = "neutral" }: AppBadgeProps) {
  return (
    <View
      style={[
        styles.badge,
        tone === "success"
          ? styles.badgeSuccess
          : tone === "warning"
            ? styles.badgeWarning
            : styles.badgeNeutral
      ]}
    >
      <Text
        style={[
          styles.badgeText,
          tone === "success"
            ? styles.badgeTextSuccess
            : tone === "warning"
              ? styles.badgeTextWarning
              : styles.badgeTextNeutral
        ]}
      >
        {label}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: "#020617"
  },
  card: {
    gap: 16,
    borderRadius: 24,
    borderWidth: 1,
    borderColor: "#1e293b",
    backgroundColor: "rgba(2, 6, 23, 0.82)",
    padding: 20
  },
  cardHeader: {
    gap: 4
  },
  cardTitle: {
    color: "#ffffff",
    fontSize: 20,
    fontWeight: "600"
  },
  cardSubtitle: {
    color: "#94a3b8",
    fontSize: 14,
    lineHeight: 20
  },
  cardBody: {
    gap: 12
  },
  button: {
    alignItems: "center",
    borderRadius: 16,
    paddingHorizontal: 16,
    paddingVertical: 12
  },
  buttonPrimary: {
    backgroundColor: "#3b82f6"
  },
  buttonSecondary: {
    borderWidth: 1,
    borderColor: "#334155",
    backgroundColor: "#0f172a"
  },
  buttonPressed: {
    opacity: 0.88
  },
  buttonDisabled: {
    opacity: 0.5
  },
  buttonText: {
    color: "#ffffff",
    fontSize: 14,
    fontWeight: "600"
  },
  badge: {
    alignSelf: "flex-start",
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 6
  },
  badgeNeutral: {
    backgroundColor: "#1e293b"
  },
  badgeSuccess: {
    backgroundColor: "rgba(16, 185, 129, 0.15)"
  },
  badgeWarning: {
    backgroundColor: "rgba(245, 158, 11, 0.15)"
  },
  badgeText: {
    fontSize: 11,
    fontWeight: "600",
    letterSpacing: 1.2,
    textTransform: "uppercase"
  },
  badgeTextNeutral: {
    color: "#e2e8f0"
  },
  badgeTextSuccess: {
    color: "#6ee7b7"
  },
  badgeTextWarning: {
    color: "#fcd34d"
  }
});
