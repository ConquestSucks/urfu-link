import type { PropsWithChildren } from "react";
import { Platform } from "react-native";
import { appConfig } from "../lib/config";

function PassThrough({ children }: PropsWithChildren) {
  return <>{children}</>;
}

export function AuthProvider({ children }: PropsWithChildren) {
  if (Platform.OS !== "web" || !appConfig.oidcAuthority || !appConfig.oidcClientId) {
    return <PassThrough>{children}</PassThrough>;
  }

  // Dynamic import avoids bundling oidc-client-ts on native
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const { AuthProvider: OidcAuthProvider } = require("react-oidc-context") as typeof import("react-oidc-context");

  return (
    <OidcAuthProvider
      authority={appConfig.oidcAuthority}
      client_id={appConfig.oidcClientId}
      redirect_uri={window.location.origin + "/"}
      scope="openid profile email"
      automaticSilentRenew
      onSigninCallback={() => {
        window.history.replaceState({}, document.title, window.location.pathname);
      }}
    >
      {children}
    </OidcAuthProvider>
  );
}
