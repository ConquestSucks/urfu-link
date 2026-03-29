import type { Href, Router } from "expo-router";

export function navigateInboxPath(router: Router, isWeb: boolean, path: Href): void {
    if (isWeb)
        router.replace(path);
    else
        router.push(path);
}
