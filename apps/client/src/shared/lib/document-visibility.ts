type VisibilityDocument = {
    addEventListener?: (event: "visibilitychange", listener: () => void) => void;
    removeEventListener?: (event: "visibilitychange", listener: () => void) => void;
    visibilityState?: string;
};

const getVisibilityDocument = (): VisibilityDocument | null => {
    if (typeof document === "undefined") {
        return null;
    }

    return document;
};

const canSubscribeToVisibility = (
    value: VisibilityDocument | null,
): value is Required<Pick<VisibilityDocument, "addEventListener" | "removeEventListener">>
    & VisibilityDocument =>
    typeof value?.addEventListener === "function" &&
    typeof value.removeEventListener === "function";

export const getDocumentVisibility = (): boolean => {
    const visibilityDocument = getVisibilityDocument();
    if (!canSubscribeToVisibility(visibilityDocument)) {
        return true;
    }

    return visibilityDocument.visibilityState === "visible";
};

export const subscribeDocumentVisibility = (
    onChange: (isVisible: boolean) => void,
): (() => void) => {
    const visibilityDocument = getVisibilityDocument();
    if (!canSubscribeToVisibility(visibilityDocument)) {
        return () => {};
    }

    const handleVisibilityChange = () => {
        onChange(visibilityDocument.visibilityState === "visible");
    };

    visibilityDocument.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
        visibilityDocument.removeEventListener("visibilitychange", handleVisibilityChange);
    };
};
