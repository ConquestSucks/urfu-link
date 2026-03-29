import { router, type Href } from "expo-router";
import { Platform } from "react-native";
export function safeGoBack(fallbackHref: Href) {
    if (Platform.OS === "web") {
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
