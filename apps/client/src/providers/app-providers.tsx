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
                retry: (failureCount, error) => {
                    if (error instanceof Response && error.status === 401) return false;
                    if (typeof error === "object" && error !== null && "status" in error && (error as { status: number }).status === 401) return false;
                    return failureCount < 1;
                },
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
