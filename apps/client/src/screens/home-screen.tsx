import { useMemo } from "react";
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { AppBadge, AppButton, AppCard, AppScreen } from "@urfu-link/ui";

import { useBackendHealth } from "../features/health/use-backend-health";
import { appConfig } from "../lib/config";
import { useSessionStore } from "../store/session-store";

export function HomeScreen() {
  const releaseTarget = useSessionStore((state) => state.releaseTarget);
  const setReleaseTarget = useSessionStore((state) => state.setReleaseTarget);
  const healthQuery = useBackendHealth();

  const healthBadge = useMemo(() => {
    if (healthQuery.isLoading || healthQuery.isFetching) {
      return { label: "Checking backend", tone: "neutral" as const };
    }

    if (healthQuery.data?.ok) {
      return { label: `Backend ready (${healthQuery.data.status})`, tone: "success" as const };
    }

    return {
      label: `Backend unavailable (${healthQuery.data?.status ?? 0})`,
      tone: "warning" as const
    };
  }, [healthQuery.data, healthQuery.isFetching, healthQuery.isLoading]);

  return (
    <AppScreen>
      <ScrollView contentContainerStyle={styles.scrollContent}>
        <View style={styles.container}>
          <View style={styles.header}>
            <AppBadge label={`${appConfig.appEnv.toUpperCase()} / ${Platform.OS.toUpperCase()}`} />
            <Text style={styles.title}>URFU Link Client</Text>
            <Text style={styles.subtitle}>
              Expo application shell for one codebase with independent web and mobile delivery.
            </Text>
          </View>

          <AppCard title="Runtime config" subtitle="Web and mobile read the same app-level config contract.">
            <InfoRow label="API base URL" value={appConfig.apiUrl} />
            <InfoRow label="Preferred release target" value={releaseTarget} />
            <InfoRow label="Build target" value={Platform.OS} />
          </AppCard>

          <AppCard title="Backend connectivity" subtitle="The first screen validates the gateway readiness contract.">
            <View style={styles.sectionBody}>
              <AppBadge label={healthBadge.label} tone={healthBadge.tone} />
              <Text style={styles.bodyText}>{healthQuery.data?.body ?? "No response yet."}</Text>
              <AppButton
                label={healthQuery.isFetching ? "Refreshing..." : "Refresh backend health"}
                onPress={() => {
                  void healthQuery.refetch();
                }}
                disabled={healthQuery.isFetching}
              />
            </View>
          </AppCard>

          <AppCard title="Release channels" subtitle="Use the same client code, but promote web and mobile independently.">
            <View style={styles.actionsRow}>
              <AppButton
                label={releaseTarget === "web" ? "Web selected" : "Use web release"}
                tone={releaseTarget === "web" ? "primary" : "secondary"}
                onPress={() => setReleaseTarget("web")}
              />
              <AppButton
                label={releaseTarget === "mobile" ? "Mobile selected" : "Use mobile release"}
                tone={releaseTarget === "mobile" ? "primary" : "secondary"}
                onPress={() => setReleaseTarget("mobile")}
              />
            </View>
          </AppCard>

          <Pressable
            accessibilityRole="link"
            onPress={() => {
              void healthQuery.refetch();
            }}
            style={styles.inlineLink}
          >
            <Text style={styles.inlineLinkText}>Refresh the gateway probe to validate dev/prod config injection.</Text>
          </Pressable>
        </View>
      </ScrollView>
    </AppScreen>
  );
}

type InfoRowProps = {
  label: string;
  value: string;
};

function InfoRow({ label, value }: InfoRowProps) {
  return (
    <View style={styles.infoRow}>
      <Text style={styles.infoLabel}>{label}</Text>
      <Text style={styles.infoValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  scrollContent: {
    flexGrow: 1,
    paddingHorizontal: 20,
    paddingVertical: 32
  },
  container: {
    width: "100%",
    maxWidth: 960,
    alignSelf: "center",
    gap: 20
  },
  header: {
    gap: 12
  },
  title: {
    color: "#ffffff",
    fontSize: 36,
    fontWeight: "600"
  },
  subtitle: {
    color: "#cbd5e1",
    fontSize: 16,
    lineHeight: 24
  },
  sectionBody: {
    gap: 12
  },
  bodyText: {
    color: "#cbd5e1",
    fontSize: 14,
    lineHeight: 22
  },
  actionsRow: {
    gap: 12
  },
  infoRow: {
    gap: 4,
    borderRadius: 16,
    backgroundColor: "rgba(15, 23, 42, 0.75)",
    paddingHorizontal: 16,
    paddingVertical: 12
  },
  infoLabel: {
    color: "#94a3b8",
    fontSize: 11,
    fontWeight: "600",
    letterSpacing: 1.2,
    textTransform: "uppercase"
  },
  infoValue: {
    color: "#f8fafc",
    fontSize: 14
  },
  inlineLink: {
    alignSelf: "flex-start"
  },
  inlineLinkText: {
    color: "#60a5fa",
    fontSize: 14,
    fontWeight: "600"
  }
});
