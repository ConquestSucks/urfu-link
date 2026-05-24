import type { Href, ImperativeRouter } from "expo-router";

export function navigateInboxPath(router: ImperativeRouter, isWeb: boolean, path: Href): void {
    if (isWeb)
        router.replace(path);
    else
        router.push(path);
}
