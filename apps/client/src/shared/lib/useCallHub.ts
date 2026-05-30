import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import { HubConnectionState } from "@microsoft/signalr";
import type { CallSessionDto } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { useCallStore } from "@/entities/call/model/call-store";

const connectHub = async (connection: ReturnType<typeof createHubConnection>) => {
    if (connection.state === HubConnectionState.Connected) {
        return;
    }

    try {
        await connection.start();
    } catch (error) {
        console.warn("CallHub connection failed", error);
    }
};

export const useCallHub = () => {
    const handleIncomingCall = useCallStore((s) => s.handleIncomingCall);
    const handleCallAccepted = useCallStore((s) => s.handleCallAccepted);
    const handleCallDeclined = useCallStore((s) => s.handleCallDeclined);
    const handleCallCancelled = useCallStore((s) => s.handleCallCancelled);
    const handleCallEnded = useCallStore((s) => s.handleCallEnded);
    const handleCallParticipantJoined = useCallStore((s) => s.handleCallParticipantJoined);
    const handleCallParticipantLeft = useCallStore((s) => s.handleCallParticipantLeft);

    useEffect(() => {
        const connection = createHubConnection("/hubs/calls");

        connection.on("IncomingCall", (call: CallSessionDto) => {
            handleIncomingCall(call);
        });

        connection.on("CallAccepted", (call: CallSessionDto) => {
            handleCallAccepted(call);
        });

        connection.on("CallDeclined", (call: CallSessionDto) => {
            handleCallDeclined(call);
        });

        connection.on("CallCancelled", (call: CallSessionDto) => {
            handleCallCancelled(call);
        });

        connection.on("CallEnded", (call: CallSessionDto) => {
            handleCallEnded(call);
        });

        connection.on("CallParticipantJoined", (callId: string, userId: string) => {
            handleCallParticipantJoined(callId, userId);
        });

        connection.on("CallParticipantLeft", (callId: string, userId: string) => {
            handleCallParticipantLeft(callId, userId);
        });

        void connectHub(connection);

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                void connectHub(connection);
            }
        });

        return () => {
            connection.off("IncomingCall");
            connection.off("CallAccepted");
            connection.off("CallDeclined");
            connection.off("CallCancelled");
            connection.off("CallEnded");
            connection.off("CallParticipantJoined");
            connection.off("CallParticipantLeft");
            subscription.remove();
            void connection.stop();
        };
    }, [
        handleCallAccepted,
        handleCallCancelled,
        handleCallDeclined,
        handleCallEnded,
        handleIncomingCall,
        handleCallParticipantJoined,
        handleCallParticipantLeft,
    ]);
};
