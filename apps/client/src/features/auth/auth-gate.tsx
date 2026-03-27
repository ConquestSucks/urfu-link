import type { PropsWithChildren } from "react";
import { useEffect } from "react";
import { Platform, StyleSheet, Text, View } from "react-native";
import { AppButton, AppScreen } from "@urfu-link/ui";
import { appConfig } from "../../lib/config";
import { setTokenAccessor } from "../../lib/api";

function useOidcAuth() {
  // Dynamic require to avoid bundling on native
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const { useAuth } = require("react-oidc-context") as typeof import("react-oidc-context");
  return useAuth();
}

export function AuthGate({ children }: PropsWithChildren) {
  if (Platform.OS !== "web" || !appConfig.oidcAuthority || !appConfig.oidcClientId) {
    return <>{children}</>;
  }

  return <WebAuthGate>{children}</WebAuthGate>;
}

function WebAuthGate({ children }: PropsWithChildren) {
  const auth = useOidcAuth();

  useEffect(() => {
    if (auth.user?.access_token) {
      setTokenAccessor(() => auth.user?.access_token);
    }
  }, [auth.user?.access_token]);

  if (auth.isLoading) {
    return (
      <AppScreen>
        <View style={styles.center}>
          <Text style={styles.text}>Loading...</Text>
        </View>
      </AppScreen>
    );
  }

  if (auth.error) {
    return (
      <AppScreen>
        <View style={styles.center}>
          <Text style={styles.title}>Authentication Error</Text>
          <Text style={styles.text}>{auth.error.message}</Text>
          <AppButton label="Try again" onPress={() => void auth.signinRedirect()} />
        </View>
      </AppScreen>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <AppScreen>
        <View style={styles.center}>
          <Text style={styles.title}>URFU Link</Text>
          <Text style={styles.text}>Sign in to continue</Text>
          <AppButton label="Sign in" onPress={() => void auth.signinRedirect()} />
        </View>
      </AppScreen>
    );
  }

  return <>{children}</>;
}

const styles = StyleSheet.create({
  center: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    gap: 16,
  },
  title: {
    color: "#ffffff",
    fontSize: 28,
    fontWeight: "600",
  },
  text: {
    color: "#cbd5e1",
    fontSize: 16,
  },
});
