type PageLifecycleHandlers = {
    onPageHide?: () => void;
    onPageShow?: () => void;
};

export function subscribePageLifecycle({ onPageHide, onPageShow }: PageLifecycleHandlers) {
    if (
        typeof window === "undefined" ||
        typeof window.addEventListener !== "function" ||
        typeof window.removeEventListener !== "function"
    ) {
        return () => undefined;
    }

    if (onPageHide) {
        window.addEventListener("pagehide", onPageHide);
    }
    if (onPageShow) {
        window.addEventListener("pageshow", onPageShow);
    }

    return () => {
        if (onPageHide) {
            window.removeEventListener("pagehide", onPageHide);
        }
        if (onPageShow) {
            window.removeEventListener("pageshow", onPageShow);
        }
    };
}
