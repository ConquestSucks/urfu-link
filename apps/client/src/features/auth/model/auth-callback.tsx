import { useAuthStore } from "@/shared/store/auth-store";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useEffect, useRef, useState } from "react";
import { ActivityIndicator, Pressable, Text, View } from "react-native";
import { exchangeNativeAuthorizationCode } from "./dev-auth-flow";

const POST_LOGIN_HREF = {
    params: {
        view: "messages",
    },
    pathname: "/chats",
} as const;

function getParam(value: string | string[] | undefined): string | null {
    if (Array.isArray(value)) {
        return value[0] ?? null;
    }

    return value ?? null;
}

export function AuthCallbackScreen() {
    const router = useRouter();
    const params = useLocalSearchParams<{
        code?: string | string[];
        error?: string | string[];
        state?: string | string[];
    }>();
    const { isTokenValid, setTokens } = useAuthStore();
    const handledRef = useRef(false);
    const [failed, setFailed] = useState(false);

    useEffect(() => {
        if (handledRef.current) {
            return;
        }

        if (isTokenValid()) {
            handledRef.current = true;
            router.replace(POST_LOGIN_HREF);
            return;
        }

        const code = getParam(params.code);
        const state = getParam(params.state);
        const error = getParam(params.error);

        if (error || !code || !state) {
            handledRef.current = true;
            setFailed(true);
            return;
        }

        handledRef.current = true;
        void exchangeNativeAuthorizationCode({ code, state })
            .then((tokens) => {
                setTokens(tokens.accessToken, tokens.refreshToken, tokens.expiresAt, tokens.idToken);
                router.replace(POST_LOGIN_HREF);
            })
            .catch((error: unknown) => {
                console.warn("[auth] Native login callback failed", error);
                setFailed(true);
            });
    }, [isTokenValid, params.code, params.error, params.state, router, setTokens]);

    return (
        <View className="flex-1 items-center justify-center gap-4 bg-app-bg px-6">
            {failed ? (
                <>
                    <Text className="text-center text-base font-semibold text-white">Login could not be completed</Text>
                    <Pressable
                        className="rounded-lg bg-app-primary px-5 py-3"
                        onPress={() => router.replace("/")}
                    >
                        <Text className="font-semibold text-white">Retry login</Text>
                    </Pressable>
                </>
            ) : (
                <ActivityIndicator size="large" />
            )}
        </View>
    );
}

export default AuthCallbackScreen;
