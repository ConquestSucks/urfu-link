import { useNotificationHub } from "@/features/notifications";
import { CallTabCoordinator } from "@/features/call/ui/CallTabCoordinator";
import { IncomingCallModal } from "@/features/call/ui/IncomingCallModal";
import { OutgoingCallModal } from "@/features/call/ui/OutgoingCallModal";
import { useCallHub } from "@/shared/lib/useCallHub";
import { usePresenceHub } from "@/shared/lib/usePresenceHub";

export default function DeferredRealtime() {
    usePresenceHub();
    useNotificationHub();
    useCallHub();

    return (
        <>
            <IncomingCallModal />
            <OutgoingCallModal />
            <CallTabCoordinator />
        </>
    );
}
