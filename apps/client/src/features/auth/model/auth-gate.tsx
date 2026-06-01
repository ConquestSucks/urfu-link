import { appConfig } from "@/shared/lib/config";
import { useAuthStore } from "@/shared/store/auth-store";
import { useRouter } from "expo-router";
import * as WebBrowser from "expo-web-browser";
import { useCallback, useEffect, useRef, useState } from "react";
import type { PropsWithChildren } from "react";
import { ActivityIndicator, Platform, Pressable, Text, View } from "react-native";
import {
    handleWebCallback,
    startNativePKCEFlow as startNativeAuthRequest,
    startWebPKCEFlow,
} from "./dev-auth-flow";

WebBrowser.maybeCompleteAuthSession();

function NativeDevAuthGate({ children }: PropsWithChildren) {
    const router = useRouter();
    const { isTokenValid } = useAuthStore();
    const [authenticating, setAuthenticating] = useState(false);
    const [promptFailed, setPromptFailed] = useState(false);
    const promptStartedRef = useRef(false);

    const startNativeLogin = useCallback(async () => {
        setPromptFailed(false);
        setAuthenticating(true);

        try {
            const result = await startNativeAuthRequest();
            if (result.type === "success" && result.params.code && result.params.state) {
                router.replace({
                    params: {
                        code: result.params.code,
                        state: result.params.state,
                    },
                    pathname: "/auth",
                });
                return;
            }

            setPromptFailed(result.type !== "opened");
        } catch {
            setPromptFailed(true);
        } finally {
            setAuthenticating(false);
        }
    }, [router]);

    useEffect(() => {
        if (isTokenValid() || promptStartedRef.current) {
            return;
        }

        promptStartedRef.current = true;
        void startNativeLogin();
    }, [isTokenValid, startNativeLogin]);

    if (authenticating || !isTokenValid()) {
        return (
            <View className="flex-1 items-center justify-center gap-4 bg-app-bg px-6">
                <ActivityIndicator size="large" />
                {promptFailed ? (
                    <Pressable
                        className="rounded-lg bg-app-primary px-5 py-3"
                        onPress={() => {
                            promptStartedRef.current = true;
                            void startNativeLogin();
                        }}
                    >
                        <Text className="font-semibold text-white">Retry login</Text>
                    </Pressable>
                ) : null}
            </View>
        );
    }

    return <>{children}</>;
}

function WebDevAuthGate({ children }: PropsWithChildren) {
    const { isTokenValid, setTokens } = useAuthStore();
    const [exchanging, setExchanging] = useState(false);

    useEffect(() => {
        const url = new URL(window.location.href);
        const code = url.searchParams.get("code");
        const state = url.searchParams.get("state");

        if (code && state) {
            // Remove OAuth params from URL before doing anything else
            window.history.replaceState({}, "", window.location.pathname);
            setExchanging(true);
            handleWebCallback(code, state)
                .then((result) => {
                    if (result) {
                        setTokens(result.accessToken, result.refreshToken, result.expiresAt, result.idToken);
                    }
                })
                .finally(() => setExchanging(false));
            return;
        }

        if (!isTokenValid()) {
            startWebPKCEFlow();
        }
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    if (exchanging || !isTokenValid()) {
        return (
            <View className="flex-1 items-center justify-center bg-app-bg">
                <ActivityIndicator size="large" />
            </View>
        );
    }

    return <>{children}</>;
}

export function AuthGate({ children }: PropsWithChildren) {
    // In production Pomerium handles auth before the request reaches the app
    if (appConfig.appEnv !== "dev") {
        return <>{children}</>;
    }

    if (Platform.OS !== "web") {
        return <NativeDevAuthGate>{children}</NativeDevAuthGate>;
    }

    return <WebDevAuthGate>{children}</WebDevAuthGate>;
}
