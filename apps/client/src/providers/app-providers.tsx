import type { PropsWithChildren } from "react";
import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { AuthProvider } from "./auth-provider";
import { AuthGate } from "../features/auth/auth-gate";
export function AppProviders({ children }: PropsWithChildren) {
    const [queryClient] = useState(() => new QueryClient({
        defaultOptions: {
            queries: {
                retry: 1,
                refetchOnWindowFocus: false,
            },
        },
    }));
    return (<SafeAreaProvider>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <AuthGate>{children}</AuthGate>
        </QueryClientProvider>
      </AuthProvider>
    </SafeAreaProvider>);
}
