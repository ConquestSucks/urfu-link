import { useNotificationHub } from "@/features/notifications";
import { ActiveCallRouteGuard } from "@/features/call/ui/ActiveCallRouteGuard";
import { CallTabCoordinator } from "@/features/call/ui/CallTabCoordinator";
import { IncomingCallModal } from "@/features/call/ui/IncomingCallModal";
import { OutgoingCallModal } from "@/features/call/ui/OutgoingCallModal";
import { useCallHub } from "@/shared/lib/useCallHub";
import { useChatHub } from "@/shared/lib/useChatHub";
import { usePresenceHub } from "@/shared/lib/usePresenceHub";

export default function DeferredRealtime() {
    useChatHub();
    usePresenceHub();
    useNotificationHub();
    useCallHub();

    return (
        <>
            <IncomingCallModal />
            <OutgoingCallModal />
            <CallTabCoordinator />
            <ActiveCallRouteGuard />
        </>
    );
}
