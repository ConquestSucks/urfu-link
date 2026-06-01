import { router, type Href } from "expo-router";
import { Platform } from "react-native";

type SafeGoBackOptions = {
    preferFallback?: boolean;
};

export function safeGoBack(fallbackHref: Href, options: SafeGoBackOptions = {}) {
    if (Platform.OS === "web" || options.preferFallback) {
        router.replace(fallbackHref);
        return;
    }
    if (router.canGoBack?.()) {
        router.back();
    }
    else {
        router.replace(fallbackHref);
    }
}
