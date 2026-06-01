import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import type { CallSessionDto } from "@urfu-link/api-client";
import { createHubConnection, HUB_CONNECTION_STATE, type HubConnection } from "@/shared/lib/signalr";
import { useCallStore } from "@/entities/call/model/call-store";
import { playMessageSound } from "@/shared/lib/message-sounds";
import { subscribePageLifecycle } from "@/shared/lib/page-lifecycle";

const connectHub = async (connection: HubConnection) => {
    if (connection.state === HUB_CONNECTION_STATE.Connected) {
        return;
    }

    try {
        await connection.start();
    } catch (error) {
        console.warn("CallHub connection failed", error);
    }
};

const isActiveDisciplineGroupCall = (call: CallSessionDto) =>
    call.status === "Active" && call.conversationId.startsWith("discipline:");

export const useCallHub = () => {
    const handleIncomingCall = useCallStore((s) => s.handleIncomingCall);
    const handleCallAccepted = useCallStore((s) => s.handleCallAccepted);
    const handleCallDeclined = useCallStore((s) => s.handleCallDeclined);
    const handleCallCancelled = useCallStore((s) => s.handleCallCancelled);
    const handleCallEnded = useCallStore((s) => s.handleCallEnded);
    const handleCallParticipantJoined = useCallStore((s) => s.handleCallParticipantJoined);
    const handleCallParticipantLeft = useCallStore((s) => s.handleCallParticipantLeft);
    const handleCallDeviceReplaced = useCallStore((s) => s.handleCallDeviceReplaced);

    useEffect(() => {
        let connection: HubConnection | null = null;
        let disposed = false;

        const setupConnection = async () => {
            const nextConnection = await createHubConnection("/hubs/calls");
            if (disposed) {
                void nextConnection.stop();
                return;
            }

            connection = nextConnection;

            nextConnection.on("IncomingCall", (call: CallSessionDto) => {
                if (isActiveDisciplineGroupCall(call)) {
                    return;
                }

                handleIncomingCall(call);
                void playMessageSound("receive", {
                    conversationId: call.conversationId,
                });
            });

            nextConnection.on("CallAccepted", (call: CallSessionDto) => {
                handleCallAccepted(call);
            });

            nextConnection.on("CallDeclined", (call: CallSessionDto) => {
                handleCallDeclined(call);
            });

            nextConnection.on("CallCancelled", (call: CallSessionDto) => {
                handleCallCancelled(call);
            });

            nextConnection.on("CallEnded", (call: CallSessionDto) => {
                handleCallEnded(call);
            });

            nextConnection.on("CallParticipantJoined", (callId: string, userId: string) => {
                handleCallParticipantJoined(callId, userId);
            });

            nextConnection.on("CallParticipantLeft", (callId: string, userId: string) => {
                handleCallParticipantLeft(callId, userId);
            });

            nextConnection.on("CallDeviceReplaced", (callId: string, clientInstanceId: string) => {
                handleCallDeviceReplaced(callId, clientInstanceId);
            });

            void connectHub(nextConnection);
        };

        void setupConnection().catch((error) => {
            console.warn("CallHub setup failed", error);
        });

        const handlePageHide = () => {
            void connection?.stop();
        };

        const handlePageShow = () => {
            if (connection) void connectHub(connection);
        };

        const unsubscribePageLifecycle = subscribePageLifecycle({
            onPageHide: handlePageHide,
            onPageShow: handlePageShow,
        });

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active" && connection) {
                void connectHub(connection);
            }
        });

        return () => {
            disposed = true;
            unsubscribePageLifecycle();
            connection?.off("IncomingCall");
            connection?.off("CallAccepted");
            connection?.off("CallDeclined");
            connection?.off("CallCancelled");
            connection?.off("CallEnded");
            connection?.off("CallParticipantJoined");
            connection?.off("CallParticipantLeft");
            connection?.off("CallDeviceReplaced");
            subscription.remove();
            void connection?.stop();
        };
    }, [
        handleCallAccepted,
        handleCallCancelled,
        handleCallDeclined,
        handleCallEnded,
        handleCallDeviceReplaced,
        handleIncomingCall,
        handleCallParticipantJoined,
        handleCallParticipantLeft,
    ]);
};
