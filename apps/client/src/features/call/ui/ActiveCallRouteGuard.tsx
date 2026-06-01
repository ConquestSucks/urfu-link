import { useEffect } from "react";
import { usePathname, useRouter, useSegments } from "expo-router";
import { useCallStore } from "@/entities/call";

export const ActiveCallRouteGuard = () => {
    const router = useRouter();
    const pathname = usePathname();
    const segments = useSegments() as string[];
    const activeCall = useCallStore((state) => state.activeCall);

    useEffect(() => {
        if (activeCall?.status !== "Active") {
            return;
        }

        const activeCallPath = `/call/${activeCall.id}`;
        const isCallRoute = segments.includes("call") && segments.includes("[id]");
        if (isCallRoute && pathname === activeCallPath) {
            return;
        }

        router.replace(activeCallPath as never);
    }, [activeCall?.id, activeCall?.status, pathname, router, segments]);

    return null;
};
