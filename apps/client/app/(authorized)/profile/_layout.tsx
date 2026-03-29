import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Redirect, Slot } from "expo-router";

export default function ProfileGroupLayout() {
    const { isMobile } = useWindowSize();

    if (!isMobile)
        return <Redirect href="/chats" />;

    return <Slot />;
}
